using System.Text.Json;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Auditoria;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Conversas;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.PendenciasCadastro;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.EsusPec;

/// <summary>Resultado de uma passagem da rotina (para log).</summary>
public sealed record ResumoCorrecaoTelefoneEsus(
    int Consultados, int Trocados, int JaCorrigidos, int EsusTambemErrado, int SemCelular, int Verificados,
    int NumeroDeOutraPessoa, int NaoAchados, int Reenvios, string? Interrompida);

/// <summary>
/// Rotina da madrugada do ADR-0067: quem teve o telefone furado (marca de telefone comprometido,
/// pendência de número errado, mensagem que falhou por número) tem o cadastro do e-SUS PEC
/// consultado — SÓ LEITURA — e, quando o PEC traz outro celular, ele vira o principal no hub
/// (o antigo vai para o histórico) e a mensagem que não chegou sai de novo, no envio normal.
///
/// <para>Antes do e-SUS, o próprio cadastro: se o principal de hoje já não é nenhum dos números que
/// falharam (alguém corrigiu depois), não há o que perguntar ao PEC — só fechar as marcas e
/// pendências que ficaram para trás e reenviar a mensagem (medido na 1ª madrugada, 05/10/2026:
/// metade das consultas foi gasta com cadastros já corrigidos, e 682 mensagens ficaram paradas).</para>
/// </summary>
public interface ICorrecaoTelefoneEsusService
{
    /// <summary>Roda uma passagem se a credencial estiver ativa e o relógio de Brasília estiver
    /// dentro da janela. Devolve null quando não entrou (fora da janela, sem credencial, nada a fazer).</summary>
    Task<ResumoCorrecaoTelefoneEsus?> ExecutarSeNaJanelaAsync(CancellationToken ct = default);
}

public sealed class CorrecaoTelefoneEsusService(
    SmsMaisDbContext db,
    IIntegracaoCredencialService credenciais,
    IPacienteFhirClient fhir,
    IContatoComprometidoService contatos,
    IPendenciaCadastroService pendencias,
    IComunicacaoPacienteService comunicacoes,
    IAuditoriaService auditoria,
    ILogger<CorrecaoTelefoneEsusService> logger) : ICorrecaoTelefoneEsusService
{
    public const string Provedor = "esuspec";
    public const string AcaoConsulta = "ConsultouEsusPec";
    public const string AcaoTroca = "TrocouTelefoneEsusPec";
    /// <summary>Valor gravado na auditoria quando o cadastro já estava corrigido (sem ir ao e-SUS).</summary>
    public const string ValorJaCorrigido = "JaCorrigido";

    /// <summary>Teto por passagem: ~3 s por paciente consultado no PEC (2 consultas + intervalo) ≈ 25 min.</summary>
    private const int MaximoPorPassagem = 500;
    /// <summary>Quem já foi consultado volta à fila só depois disso — o PEC não muda toda noite.</summary>
    private static readonly TimeSpan Reconsulta = TimeSpan.FromDays(7);
    /// <summary>Só olha falhas de envio recentes; as antigas já não têm mensagem a reenviar.</summary>
    private static readonly TimeSpan JanelaFalhas = TimeSpan.FromDays(60);
    /// <summary>
    /// Folga mínima até o atendimento para reenviar: a rotina roda de madrugada e a mensagem só sai no
    /// horário de envio — atendimento às 07h rearmado às 03h chegaria depois. O enviador confere de
    /// novo na hora de enviar ("sem data futura no momento do envio").
    /// </summary>
    private static readonly TimeSpan FolgaReenvio = TimeSpan.FromHours(6);
    private const int ErrosSeguidosParaDesistir = 5;

    /// <summary>
    /// Noite (data de Brasília) em que os já consultados nesta semana tiveram o cadastro reconferido.
    /// Uma vez por noite basta — sem isso, a cada 15 min a mesma lista seria relida do hub e tomaria
    /// o lugar de quem ainda espera o PEC. Estático: vale para o processo; reiniciar só repete a conferência.
    /// </summary>
    private static DateOnly? _ultimaReconferencia;

    private static readonly TimeOnly JanelaInicioPadrao = new(2, 0);
    private static readonly TimeOnly JanelaFimPadrao = new(5, 0);

    public async Task<ResumoCorrecaoTelefoneEsus?> ExecutarSeNaJanelaAsync(CancellationToken ct = default)
    {
        IntegracaoCredencialContexto cred;
        try { cred = await credenciais.ObterContextoAsync(Provedor, ct); }
        catch (ValidacaoException) { return null; } // não configurada / desativada
        if (string.IsNullOrWhiteSpace(cred.ClientId) || string.IsNullOrWhiteSpace(cred.ClientSecret)) return null;

        var p = Parametros.Ler(cred.ParametrosJson);
        if (!DentroDaJanela(p.Inicio, p.Fim, AgoraBrasilia())) return null;

        var noite = FusoBrasilia.HojeEmBrasilia();
        var reconferir = _ultimaReconferencia != noite;
        var candidatos = await CandidatosAsync(reconferir, ct);
        if (reconferir) _ultimaReconferencia = noite;
        if (candidatos.Count == 0) return null;

        logger.LogInformation("Correção de telefone pelo e-SUS: {Qtd} paciente(s) na fila desta passagem.", candidatos.Count);

        int consultados = 0, trocados = 0, jaCorrigidos = 0, tambemErrado = 0, semCelular = 0, verificados = 0,
            deOutro = 0, naoAchados = 0, reenvios = 0, errosSeguidos = 0;
        string? interrompida = null;

        // O login no PEC só acontece se algum paciente precisar dele — passagem só de cadastros já
        // corrigidos não encosta no e-SUS.
        using var pec = new EsusPecCliente(p.BaseUrl);
        var logado = false;
        async Task<EsusPecCliente> PecAsync()
        {
            if (!logado)
            {
                // De madrugada ninguém está usando a conta: forçar é seguro (e é o motivo da janela).
                try
                {
                    await pec.LoginAsync(cred.ClientId, cred.ClientSecret, forcar: true, ct);
                    await pec.SelecionarAcessoAsync(p.AcessoId, ct);
                }
                catch (ErroEsusPec e) { throw new SessaoPecFalhou(e.Message); }
                logado = true;
            }
            return pec;
        }

        try
        {
            foreach (var c in candidatos)
            {
                if (!DentroDaJanela(p.Inicio, p.Fim, AgoraBrasilia())) { interrompida = "fim da janela"; break; }
                try
                {
                    var r = await ProcessarAsync(PecAsync, c, ct);
                    errosSeguidos = 0;
                    if (r.ConsultouPec) consultados++;
                    reenvios += r.Reenvios;
                    switch (r.Decisao)
                    {
                        case DecisaoTelefoneEsus.Trocar: trocados++; break;
                        case DecisaoTelefoneEsus.JaEOPrincipal: jaCorrigidos++; break;
                        case DecisaoTelefoneEsus.EsusTambemErrado: tambemErrado++; break;
                        case DecisaoTelefoneEsus.EsusSemCelular: semCelular++; break;
                        case DecisaoTelefoneEsus.MantemVerificado: verificados++; break;
                        case DecisaoTelefoneEsus.NumeroDeOutraPessoa: deOutro++; break;
                        case null when r.ConsultouPec: naoAchados++; break;
                    }
                }
                catch (SessaoPecFalhou) { throw; } // sem sessão no PEC não adianta seguir
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    db.ChangeTracker.Clear();
                    logger.LogWarning(ex, "Correção de telefone pelo e-SUS: falha no paciente {Paciente}.", c.PacienteId);
                    if (++errosSeguidos >= ErrosSeguidosParaDesistir) { interrompida = $"{errosSeguidos} erros seguidos"; break; }
                }
            }
        }
        catch (SessaoPecFalhou ex)
        {
            interrompida = ex.Message;
            logger.LogWarning("Correção de telefone pelo e-SUS: sessão no PEC falhou — {Erro}", ex.Message);
        }
        finally
        {
            if (logado) await pec.LogoutAsync(CancellationToken.None);
        }

        var resumo = new ResumoCorrecaoTelefoneEsus(consultados, trocados, jaCorrigidos, tambemErrado, semCelular,
            verificados, deOutro, naoAchados, reenvios, interrompida);
        logger.LogInformation("Correção de telefone pelo e-SUS: {@Resumo}", resumo);
        return resumo;
    }

    // ------------------------------------------------------------------------------------------

    /// <summary>Login/escolha de acesso no PEC falhou — encerra a passagem (erro de um paciente não encerra).</summary>
    private sealed class SessaoPecFalhou(string mensagem) : Exception(mensagem);

    /// <param name="ConsultadoRecente">Já passou pelo PEC nos últimos 7 dias — não é consultado de novo.</param>
    /// <param name="TemPendenteLocal">Tem algo que o cadastro corrigido resolve sozinho: marca aberta,
    /// pendência aberta ou mensagem de atendimento futuro parada por número.</param>
    internal sealed record Candidato(
        Guid PacienteId, IReadOnlyList<string> Furados, bool TemAgendamentoFuturo,
        bool ConsultadoRecente, bool TemPendenteLocal);

    private sealed record Resultado(DecisaoTelefoneEsus? Decisao, bool ConsultouPec, int Reenvios = 0);

    /// <summary>
    /// Quem tem telefone furado: marca de telefone comprometido aberta, pendência de número errado
    /// aberta, ou mensagem recente que falhou por número (erro permanente da Meta / sem telefone /
    /// retida por número negado). Entra quem ainda não foi ao PEC nesta semana, ou quem tem algo que
    /// o cadastro já corrigido resolve sem PEC. Quem tem agendamento pela frente vai primeiro.
    /// </summary>
    private async Task<IReadOnlyList<Candidato>> CandidatosAsync(bool reconferir, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var desdeFalha = agora - JanelaFalhas;
        var limiteFuturo = agora + FolgaReenvio;

        var comprometidos = await db.ContatosComprometidos.AsNoTracking()
            .Where(c => c.ResolvidoEm == null)
            .Select(c => new { c.PacienteId, Telefone = (string?)c.TelefoneCanonical })
            .ToListAsync(ct);
        var negados = await db.PendenciasCadastro.AsNoTracking()
            .Where(x => x.Status == StatusPendenciaCadastro.Aberta && x.Tipo == TipoPendenciaCadastro.NumeroErrado
                && x.PacienteId != null)
            .Select(x => new { PacienteId = x.PacienteId!.Value, Telefone = (string?)x.TelefoneCanonical })
            .ToListAsync(ct);
        var falhas = await db.ComunicacoesPaciente.AsNoTracking()
            .Where(FalhaDeNumero)
            .Where(c => c.CriadoEm > desdeFalha)
            .Select(c => new { c.PacienteId, c.Telefone })
            .ToListAsync(ct);
        var falhasFuturas = (await db.ComunicacoesPaciente.AsNoTracking()
                .Where(FalhaDeNumero)
                .Where(c => c.SolicitacaoId != null
                    && c.Solicitacao!.ExcluidoEm == null
                    && c.Solicitacao.Status != StatusSolicitacao.Cancelada
                    && c.Solicitacao.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente
                    && c.Solicitacao.DataAgendada > limiteFuturo)
                .Select(c => c.PacienteId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        var porPaciente = comprometidos.Concat(negados).Concat(falhas)
            .GroupBy(x => x.PacienteId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Telefone).OfType<string>()
                .Where(t => t.Length > 0).Distinct().ToList());
        if (porPaciente.Count == 0) return [];

        var comMarca = comprometidos.Select(x => x.PacienteId).Concat(negados.Select(x => x.PacienteId)).ToHashSet();

        var desdeConsulta = agora - Reconsulta;
        var jaConsultados = (await db.RegistrosAuditoria.AsNoTracking()
                .Where(a => a.Acao == AcaoConsulta && a.CriadoEm > desdeConsulta)
                .Select(a => a.EntidadeId)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var ids = porPaciente.Keys.ToList();
        var comFuturo = (await db.Solicitacoes.AsNoTracking()
                .Where(s => ids.Contains(s.PacienteId) && s.ExcluidoEm == null
                    && s.Status != StatusSolicitacao.Cancelada && s.DataAgendada > agora)
                .Select(s => s.PacienteId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        var todos = ids
            .Select(id => new Candidato(id, porPaciente[id], comFuturo.Contains(id),
                jaConsultados.Contains(id.ToString()), comMarca.Contains(id) || falhasFuturas.Contains(id)))
            .ToList();

        // Os já consultados nesta semana só voltam para a reconferência do cadastro (uma vez por
        // noite, sem PEC, fora do teto); a fila do PEC é a dos que ainda não foram consultados.
        var reconferencia = reconferir
            ? todos.Where(c => c.ConsultadoRecente && c.TemPendenteLocal)
            : [];
        var paraPec = todos.Where(c => !c.ConsultadoRecente)
            .OrderByDescending(c => c.TemAgendamentoFuturo)
            .Take(MaximoPorPassagem);
        return [.. reconferencia.OrderByDescending(c => c.TemAgendamentoFuturo), .. paraPec];
    }

    /// <summary>Mensagem que falhou por causa do NÚMERO (não por "já respondeu", "sem data" etc.).</summary>
    private static readonly System.Linq.Expressions.Expression<Func<ComunicacaoPaciente, bool>> FalhaDeNumero = c =>
        c.Status == StatusComunicacao.AguardandoCorrecaoContato
        || c.Status == StatusComunicacao.SemTelefoneValido
        || (c.Status == StatusComunicacao.Falha && c.MotivoFalha != null
            && (c.MotivoFalha.Contains("131026") || c.MotivoFalha.Contains("131030")));

    private async Task<Resultado> ProcessarAsync(Func<Task<EsusPecCliente>> obterPec, Candidato c, CancellationToken ct)
    {
        var patient = await fhir.ObterAsync(c.PacienteId, ct);
        if (patient is null) return new Resultado(null, false);

        // 1) O cadastro já foi corrigido depois da falha: nada a perguntar ao e-SUS — arruma o que
        //    ficou para trás (marcas, pendências, mensagem parada) com o número de hoje.
        if (TrocaTelefoneEsus.JaCorrigido(patient, c.Furados))
        {
            var principal = TrocaTelefoneEsus.CelularNacional(TrocaTelefoneEsus.Principal(patient)!.Value)!;
            var reenviados = await ArrumarDepoisDaCorrecaoAsync(c.PacienteId, principal,
                "Telefone já corrigido no cadastro", ct);
            if (!c.ConsultadoRecente)
                await RegistrarConsultaAsync(c.PacienteId, ValorJaCorrigido, principal, ct);
            return new Resultado(DecisaoTelefoneEsus.JaEOPrincipal, false, reenviados);
        }

        // Daqui para baixo é o PEC — e ele só é perguntado uma vez por semana.
        if (c.ConsultadoRecente) return new Resultado(null, false);

        // Validado (OTP) não se altera — nem gasta consulta no PEC.
        if (TrocaTelefoneEsus.TemTelefoneValidado(patient))
        {
            await RegistrarConsultaAsync(c.PacienteId, nameof(DecisaoTelefoneEsus.MantemVerificado), null, ct);
            return new Resultado(DecisaoTelefoneEsus.MantemVerificado, false);
        }

        var pec = await obterPec();
        var cpf = PacienteFhirMapper.CpfDe(patient);
        var cns = PacienteFhirMapper.CnsDe(patient);
        CidadaoEsusPec? cidadao = null;
        if (!string.IsNullOrEmpty(cpf)) cidadao = await pec.BuscarCidadaoAsync(cpf, ct);
        if (cidadao is null && !string.IsNullOrEmpty(cns)) cidadao = await pec.BuscarCidadaoAsync(cns, ct);
        if (cidadao is null)
        {
            await RegistrarConsultaAsync(c.PacienteId, "NaoAchado", null, ct);
            return new Resultado(null, true);
        }

        var celular = TrocaTelefoneEsus.CelularNacional(cidadao.TelefoneCelular);
        var deOutro = celular is not null && await ConfirmadoPorOutraPessoaAsync(patient, c.PacienteId, cpf, celular, ct);
        var decisao = TrocaTelefoneEsus.Decidir(patient, celular, c.Furados, deOutro);

        switch (decisao)
        {
            case DecisaoTelefoneEsus.Trocar:
                var reenvios = await TrocarAsync(c, celular!, ct);
                return new Resultado(decisao, true, reenvios);

            case DecisaoTelefoneEsus.EsusTambemErrado:
                // O e-SUS tem o MESMO número furado: fica anotado na marca, para a recepção saber que
                // não adianta procurar lá — o jeito é pegar o número na próxima vinda.
                await contatos.MarcarAsync(c.PacienteId, celular, MotivoContatoComprometido.NaoEhWhatsApp,
                    "O e-SUS (atenção básica) tem este mesmo número.", ct: ct);
                break;
        }

        await RegistrarConsultaAsync(c.PacienteId, decisao.ToString(), celular, ct);
        return new Resultado(decisao, true);
    }

    /// <summary>O celular do PEC é o confirmado (próprio) de outra pessoa sem sobrenome em comum?
    /// Mesma régua da tela de verificação: vínculo próprio + CPF diferente; família (sobrenome em
    /// comum) passa — é o caso comum do celular da mãe.</summary>
    private async Task<bool> ConfirmadoPorOutraPessoaAsync(
        Patient patient, Guid pacienteId, string? cpf, string celular, CancellationToken ct)
    {
        var bundle = await fhir.BuscarAsync(telecom: celular, ct: ct);
        var nome = PacienteFhirMapper.NomeDe(patient);
        return bundle.Entry.Select(e => e.Resource).OfType<Patient>().Any(o =>
            o.Id != pacienteId.ToString()
            && PatientMergeFhir.TelefoneEstaConfirmado(o, celular)
            && PatientMergeFhir.VinculoContatoConfirmado(o) == VinculoContatoVerificado.Proprio
            && PacienteFhirMapper.CpfDe(o) is { Length: 11 } outroCpf && outroCpf != cpf
            && !TrocaTelefoneEsus.SobrenomeEmComum(nome, PacienteFhirMapper.NomeDe(o)));
    }

    private async Task<int> TrocarAsync(Candidato c, string celular, CancellationToken ct)
    {
        var agora = DateTimeOffset.UtcNow;
        string? anterior = null;
        var aplicou = false;
        for (var tentativa = 1; ; tentativa++)
        {
            var patient = await fhir.ObterAsync(c.PacienteId, ct)
                ?? throw new NaoEncontradoException("Paciente", c.PacienteId);
            if (TrocaTelefoneEsus.TemTelefoneValidado(patient)) break; // verificaram no meio do caminho
            (_, anterior) = TrocaTelefoneEsus.AplicarTroca(patient, celular, c.Furados, agora);
            try
            {
                await fhir.AtualizarAsync(c.PacienteId, patient, ct);
                aplicou = true;
                break;
            }
            catch (ConflitoVersaoHubException) when (tentativa < 3)
            {
                // Alguém mexeu no cadastro entre a leitura e a gravação — relê e reaplica.
            }
        }
        if (!aplicou) return 0;

        await auditoria.RegistrarAsync("Paciente", c.PacienteId.ToString(), AcaoTroca, anterior, celular, ct);
        await RegistrarConsultaAsync(c.PacienteId, nameof(DecisaoTelefoneEsus.Trocar), celular, ct);
        return await ArrumarDepoisDaCorrecaoAsync(c.PacienteId, celular,
            "Telefone corrigido pelo e-SUS (atenção básica)", ct);
    }

    /// <summary>
    /// Com o número de hoje (<paramref name="atual"/>) valendo: fecha as marcas de telefone
    /// comprometido que falavam de outro número, resolve as pendências de número errado (o que já
    /// solta as mensagens retidas) e reenvia a mensagem que falhou por número.
    /// </summary>
    private async Task<int> ArrumarDepoisDaCorrecaoAsync(Guid pacienteId, string atual, string motivo, CancellationToken ct)
    {
        await contatos.ReconciliarAsync(pacienteId, atual, ct);
        await db.SaveChangesAsync(ct);

        var abertas = await db.PendenciasCadastro.AsNoTracking()
            .Where(x => x.PacienteId == pacienteId && x.Status == StatusPendenciaCadastro.Aberta
                && x.Tipo == TipoPendenciaCadastro.NumeroErrado)
            .Select(x => new { x.Id, x.TelefoneCanonical })
            .ToListAsync(ct);
        foreach (var x in abertas.Where(x => !TelefoneWhatsApp.MesmoNumero(x.TelefoneCanonical, atual)))
            await pendencias.ResolverAsync(x.Id, $"{motivo}: {atual}.", ct);

        return await RearmarFalhasAsync(pacienteId, atual, motivo, ct);
    }

    /// <summary>
    /// A mensagem que falhou por número volta à fila — mas SÓ se o atendimento ainda está pela frente
    /// (com folga para a mensagem sair no horário de envio), não foi confirmado nem cancelado. Mesmo
    /// saneamento do reenvio manual: links antigos revogados, linha rearmada; o enviador resolve o
    /// telefone do cadastro (já corrigido) e confere a data de novo na hora de enviar. Fica a anotação
    /// no card.
    /// </summary>
    private async Task<int> RearmarFalhasAsync(Guid pacienteId, string atual, string motivo, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var limiteFuturo = agora + FolgaReenvio;
        var ultimas = (await db.ComunicacoesPaciente
                .Include(x => x.Solicitacao)
                .Where(x => x.PacienteId == pacienteId && x.SolicitacaoId != null
                    && x.Solicitacao!.ExcluidoEm == null
                    && x.Solicitacao.Status != StatusSolicitacao.Cancelada
                    && x.Solicitacao.DataAgendada > limiteFuturo
                    && x.Solicitacao.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente)
                .ToListAsync(ct))
            .GroupBy(x => (x.SolicitacaoId, x.Finalidade))
            .Select(g => g.OrderByDescending(x => x.CriadoEm).First())
            .Where(x => x.Status == StatusComunicacao.SemTelefoneValido
                || (x.Status == StatusComunicacao.Falha && ComunicacaoPacienteService.ErroPermanente(x.MotivoFalha))
                // Retida por número negado, mas o número de hoje é outro: a retenção não vale mais.
                || (x.Status == StatusComunicacao.AguardandoCorrecaoContato && !TelefoneWhatsApp.MesmoNumero(x.Telefone, atual)))
            .ToList();

        foreach (var n in ultimas)
        {
            await comunicacoes.RevogarAcessosAsync(n.SolicitacaoId!.Value, agora, ct);
            ComunicacaoPacienteService.RearmarParaNovoEnvio(n);
            db.ContatosRegistro.Add(new ContatoRegistro
            {
                Id = Guid.NewGuid(),
                SolicitacaoId = n.SolicitacaoId.Value,
                PacienteId = pacienteId,
                Meio = MeioContato.WhatsApp,
                Resultado = ResultadoContato.Outro,
                Observacao = $"A mensagem não chegou no número antigo. {motivo} para {atual} e a mensagem foi reenviada.",
                CriadoEm = agora,
            });
        }
        if (ultimas.Count > 0) await db.SaveChangesAsync(ct);
        return ultimas.Count;
    }

    private async Task RegistrarConsultaAsync(Guid pacienteId, string valor, string? celular, CancellationToken ct)
    {
        await auditoria.RegistrarAsync("Paciente", pacienteId.ToString(), AcaoConsulta, null,
            celular is null ? valor : $"{valor}: {celular}", ct);
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------------------------------

    private static TimeOnly AgoraBrasilia() => TimeOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));

    /// <summary>Dentro da janela [início, fim) — aceita janela que vira a meia-noite (ex.: 23:00–02:00).</summary>
    internal static bool DentroDaJanela(TimeOnly inicio, TimeOnly fim, TimeOnly agora) =>
        inicio <= fim ? agora >= inicio && agora < fim : agora >= inicio || agora < fim;

    internal sealed record Parametros(string? BaseUrl, string? AcessoId, TimeOnly Inicio, TimeOnly Fim)
    {
        public static Parametros Ler(string? json)
        {
            string? Texto(JsonElement r, string k) =>
                r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;
            TimeOnly Hora(string? s, TimeOnly padrao) => TimeOnly.TryParseExact(s, "HH:mm", out var t) ? t : padrao;

            if (string.IsNullOrWhiteSpace(json)) return new(null, null, JanelaInicioPadrao, JanelaFimPadrao);
            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                return new(Texto(r, "baseUrl"), Texto(r, "acessoId"),
                    Hora(Texto(r, "janelaInicio"), JanelaInicioPadrao), Hora(Texto(r, "janelaFim"), JanelaFimPadrao));
            }
            catch (JsonException)
            {
                return new(null, null, JanelaInicioPadrao, JanelaFimPadrao);
            }
        }
    }
}

/// <summary>
/// Acorda de 15 em 15 minutos; o serviço só entra quando a credencial está ativa, o relógio está
/// na janela da madrugada e há alguém na fila — fora disso não faz nem login no e-SUS.
/// </summary>
public sealed class CorrecaoTelefoneEsusWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<CorrecaoTelefoneEsusWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ICorrecaoTelefoneEsusService>()
                    .ExecutarSeNaJanelaAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                // Nunca deixa subir: BackgroundServiceExceptionBehavior=StopHost derrubaria a API.
                logger.LogError(ex, "Falha na correção de telefone pelo e-SUS — tenta na próxima volta.");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
