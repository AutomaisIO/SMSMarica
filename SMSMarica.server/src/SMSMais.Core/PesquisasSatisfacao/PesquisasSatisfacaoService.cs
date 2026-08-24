using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SMSMais.Core.Atendimentos;
using SMSMais.Core.Atendimentos.Fhir;
using Hl7.Fhir.Model;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Conversas;
using SMSMais.Core.Identidade;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Pacientes;
using SMSMais.Core.PesquisasSatisfacao.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.PesquisasSatisfacao;

/// <summary>
/// Pesquisa de satisfação por atendimento. Ver <see cref="PesquisaSatisfacao"/> para o desenho
/// do token e <see cref="IPesquisasSatisfacaoService.JanelaDias"/> para o prazo.
/// </summary>
public sealed class PesquisasSatisfacaoService(
    SmsMaisDbContext db,
    IAtendimentosService atendimentos,
    IEncounterFhirClient fhir,
    IUsuarioAtualAccessor usuarioAtual,
    IPacientesService pacientes,
    IWhatsAppCliente whatsApp,
    IConfiguration configuration) : IPesquisasSatisfacaoService
{
    public async Task<EnvioPesquisaDto> PrepararEnvioAsync(
        Guid pacienteId, Guid encounterId, CancellationToken ct = default)
    {
        var p = await db.PesquisasSatisfacao.FirstOrDefaultAsync(x => x.EncounterId == encounterId, ct);
        var jaEnviada = p?.EnviadaEm is not null;

        if (p is null)
        {
            p = await CriarAsync(pacienteId, encounterId, ct);
        }
        else if (p.PatientId != pacienteId)
        {
            // O atendimento é de outra pessoa: enviar aqui mandaria o convite ao paciente errado.
            throw new ConflitoException(
                "pesquisa.paciente_divergente",
                "Este atendimento pertence a outro paciente.");
        }

        if (DateTime.UtcNow > p.ExpiraEm)
            throw new ConflitoException(
                "pesquisa.fora_da_janela",
                $"O prazo de {IPesquisasSatisfacaoService.JanelaDias} dias para avaliar este atendimento já passou.");

        p.EnviadaEm = DateTime.UtcNow;
        p.EnviadaPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);

        return new EnvioPesquisaDto(p.Id, MontarUrl(p.Id), jaEnviada);
    }

    public async Task<EnvioPesquisaDto> EnviarAsync(
        Guid pacienteId, Guid encounterId, CancellationToken ct = default)
    {
        var envio = await PrepararEnvioAsync(pacienteId, encounterId, ct);
        var p = await db.PesquisasSatisfacao.AsNoTracking().FirstAsync(x => x.Id == envio.PesquisaId, ct);
        var paciente = await pacientes.ObterPorIdAsync(pacienteId, ct);

        // Mesma régua do resto das comunicações: contato verificado primeiro, qualquer celular
        // do cadastro depois. Aqui NÃO se exige verificação — a pesquisa não carrega resultado
        // nem laudo, e exigir contato verificado deixaria de fora justamente quem a recepção
        // ainda não alcançou.
        var telefone = paciente.TelefoneVerificado
            ?? new[] { paciente.TelefoneCelular, paciente.TelefonePrincipal, paciente.TelefoneResidencial }
                .FirstOrDefault(TelefoneWhatsApp.EhCelularBr);

        if (!TelefoneWhatsApp.EhCelularBr(telefone))
            throw new ConflitoException(
                "pesquisa.sem_telefone", "Paciente sem número de celular válido para receber a pesquisa.");

        // O corpo do template diz "recebido em nossa unidade {{2}}". Sem o nome real, a frase
        // sairia "em nossa unidade nossa unidade" — e, pior, afirmaria ao paciente algo que não
        // conferimos. Recusa é o comportamento certo: a unidade vem do serviceProvider do
        // Encounter (ADR-0039) e está preenchida em 100% dos atendimentos recentes; se faltar,
        // é sinal de dado incompleto, não de mensagem a improvisar.
        if (string.IsNullOrWhiteSpace(p.UnidadeNome))
            throw new ConflitoException(
                "pesquisa.sem_unidade",
                "O atendimento não tem unidade registrada — não dá para citar o local na mensagem.");

        var template = configuration["Pesquisa:Template"] ?? "pesquisa_de_satisfacao_uri";
        var idioma = configuration["Pesquisa:TemplateIdioma"] ?? "pt_BR";

        var resultado = await whatsApp.EnviarTemplateComBotoesAsync(
            TelefoneWhatsApp.NormalizarNonoDigito(telefone!),
            template,
            idioma,
            [
                Tratamento(paciente.NomeCompleto, paciente.Sexo),
                p.UnidadeNome,
                FusoBrasilia.ParaExibicao(p.AtendimentoEm).ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR")),
            ],
            // Só o botão de URL entra no payload: as duas respostas rápidas do template são
            // estáticas. O índice 0 é o do botão de link, que é o primeiro do template aprovado.
            [new BotaoTemplateWhatsApp(TipoBotaoTemplate.Url, envio.PesquisaId.ToString())],
            pacienteId,
            ct);

        if (!resultado.Ok)
            throw new ConflitoException("pesquisa.falha_envio", resultado.Erro ?? "Falha ao enviar a pesquisa.");

        // O wamid é a ponte para `whatsapp_mensagem`, onde o webhook grava entregue/lida. Sem
        // ele o painel não teria "vistas" — e duplicar esse estado aqui só criaria divergência.
        var linha = await db.PesquisasSatisfacao.FirstAsync(x => x.Id == envio.PesquisaId, ct);
        linha.WaMessageId = resultado.WaMessageId;
        linha.PacienteSexo = paciente.Sexo;
        linha.PacienteNascimento = paciente.DataNascimento;
        await db.SaveChangesAsync(ct);

        return envio;
    }

    /// <summary>Id de uma referência FHIR (<c>Patient/{guid}</c>). Null quando não dá para ler.</summary>
    private static Guid? IdDaReferencia(string? referencia)
    {
        var ultimo = referencia?.Split('/').LastOrDefault();
        return Guid.TryParse(ultimo, out var id) ? id : null;
    }

    /// <summary>Sr./Sra. + primeiro nome; sem sexo no cadastro, só o primeiro nome.</summary>
    private static string Tratamento(string? nomeCompleto, Sexo? sexo)
    {
        var primeiro = (nomeCompleto ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? "paciente";
        return sexo switch
        {
            Sexo.Masculino => $"Sr. {primeiro}",
            Sexo.Feminino => $"Sra. {primeiro}",
            _ => primeiro,
        };
    }

    public async Task<string> RegistrarCliqueAsync(Guid token, CancellationToken ct = default)
    {
        var p = await db.PesquisasSatisfacao.FirstOrDefaultAsync(x => x.Id == token, ct)
            ?? throw new NaoEncontradoException("Pesquisa de satisfação", token);

        var destino = await DestinoDaUnidadeAsync(p.UnidadeCnes, ct);

        // Conta mesmo fora da janela: o clique aconteceu, e esconder isso do painel só produz
        // taxa de engajamento errada. Quem decide se ainda aceita resposta é a AvanteSocial.
        p.ClicadaEm ??= DateTime.UtcNow;
        p.Cliques++;
        await db.SaveChangesAsync(ct);

        return destino;
    }

    /// <summary>
    /// Link da AvanteSocial configurado para a unidade do atendimento, resolvido por CNES —
    /// a mesma chave que o ADR-0039 usa para casar unidade entre PEPs, e a que a planilha da
    /// AvanteSocial traz.
    /// </summary>
    private async Task<string> DestinoDaUnidadeAsync(string? cnes, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cnes))
            throw new ConflitoException(
                "pesquisa.sem_cnes", "O atendimento não tem unidade identificada por CNES.");

        var destino = await (
            from u in db.Unidades.AsNoTracking()
            join c in db.UnidadePesquisaConfigs.AsNoTracking() on u.Id equals c.UnidadeId
            where u.Cnes == cnes
            select c.LinkResponder).FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(destino))
            throw new ConflitoException(
                "pesquisa.sem_link",
                "A unidade deste atendimento não tem link de pesquisa configurado.");

        return destino;
    }

    public async Task<PesquisaConfigDto> ObterConfigAsync(Guid unidadeId, CancellationToken ct = default)
    {
        var u = await db.Unidades.AsNoTracking().FirstOrDefaultAsync(x => x.Id == unidadeId, ct)
            ?? throw new NaoEncontradoException("Unidade", unidadeId);
        var c = await db.UnidadePesquisaConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.UnidadeId == unidadeId, ct);

        return new PesquisaConfigDto(
            u.Id, u.Nome, u.Cnes,
            c?.EnvioWhatsAppAtivo ?? false,
            c?.LinkResponder, c?.LinkPainel,
            c?.HorasAposAtendimento ?? 24,
            c?.AtualizadoEm);
    }

    public async Task<PesquisaConfigDto> SalvarConfigAsync(
        Guid unidadeId, SalvarPesquisaConfigRequest request, CancellationToken ct = default)
    {
        _ = await db.Unidades.AsNoTracking().FirstOrDefaultAsync(x => x.Id == unidadeId, ct)
            ?? throw new NaoEncontradoException("Unidade", unidadeId);

        // Ligar o disparo sem destino manda o paciente para lugar nenhum: o redirect recusaria e
        // ele receberia uma mensagem que não abre. Melhor barrar aqui, onde dá para explicar.
        if (request.EnvioWhatsAppAtivo && string.IsNullOrWhiteSpace(request.LinkResponder))
            throw new ValidacaoException(
                "linkResponder", "Informe o link da pesquisa antes de ligar o envio por WhatsApp.");

        if (request.HorasAposAtendimento is < 1 or > 168)
            throw new ValidacaoException(
                "horasAposAtendimento", "O atraso do envio deve ficar entre 1 hora e 7 dias.");

        var c = await db.UnidadePesquisaConfigs.FirstOrDefaultAsync(x => x.UnidadeId == unidadeId, ct);
        if (c is null)
        {
            c = new UnidadePesquisaConfig { UnidadeId = unidadeId };
            db.UnidadePesquisaConfigs.Add(c);
        }

        c.EnvioWhatsAppAtivo = request.EnvioWhatsAppAtivo;
        c.LinkResponder = string.IsNullOrWhiteSpace(request.LinkResponder) ? null : request.LinkResponder.Trim();
        c.LinkPainel = string.IsNullOrWhiteSpace(request.LinkPainel) ? null : request.LinkPainel.Trim();
        c.HorasAposAtendimento = request.HorasAposAtendimento;
        c.AtualizadoEm = DateTime.UtcNow;
        c.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);

        return await ObterConfigAsync(unidadeId, ct);
    }

    public async Task<PesquisaPainelDto> ObterPainelAsync(Guid unidadeId, int dias, CancellationToken ct = default)
    {
        var u = await db.Unidades.AsNoTracking().FirstOrDefaultAsync(x => x.Id == unidadeId, ct)
            ?? throw new NaoEncontradoException("Unidade", unidadeId);
        var desde = DateTime.UtcNow.AddDays(-Math.Clamp(dias, 1, 365));

        var convites = await db.PesquisasSatisfacao.AsNoTracking()
            .Where(p => p.UnidadeCnes == u.Cnes && p.EnviadaEm != null && p.EnviadaEm >= desde)
            .Select(p => new
            {
                p.WaMessageId, p.ClicadaEm, p.EnviadaEm, p.PacienteSexo, p.PacienteNascimento, p.AtendimentoEm,
            })
            .ToListAsync(ct);

        // Entregue/lida vêm de `whatsapp_mensagem`, alimentada pelo webhook. Não duplicamos esse
        // estado aqui: duas fontes para o mesmo fato divergem, e a do webhook é a verdadeira.
        var wamids = convites.Select(c => c.WaMessageId).Where(w => w != null).ToList();
        var status = await db.MensagensWhatsApp.AsNoTracking()
            .Where(m => m.WaMessageId != null && wamids.Contains(m.WaMessageId))
            .Select(m => m.Status)
            .ToListAsync(ct);

        var clicados = convites.Where(c => c.ClicadaEm is not null).ToList();
        var horas = clicados
            .Where(c => c.EnviadaEm is not null)
            .Select(c => (c.ClicadaEm!.Value - c.EnviadaEm!.Value).TotalHours)
            .OrderBy(h => h).ToList();

        var perfil = clicados
            .GroupBy(c => new
            {
                Sexo = c.PacienteSexo?.ToString() ?? "Não informado",
                Faixa = FaixaEtaria(c.PacienteNascimento, c.AtendimentoEm),
            })
            .Select(g => new PesquisaPerfilDto(g.Key.Sexo, g.Key.Faixa, g.Count()))
            .OrderByDescending(x => x.Cliques)
            .ToList();

        return new PesquisaPainelDto(
            Enviadas: convites.Count,
            Entregues: status.Count(s => s is StatusMensagemWhatsApp.Entregue or StatusMensagemWhatsApp.Lida),
            Vistas: status.Count(s => s == StatusMensagemWhatsApp.Lida),
            Clicadas: clicados.Count,
            HorasMedianasAteClique: horas.Count == 0 ? null : Math.Round(horas[horas.Count / 2], 1),
            Perfil: perfil);
    }

    /// <summary>Faixas amplas de propósito: com poucas respostas, faixa estreita identifica gente.</summary>
    private static string FaixaEtaria(DateOnly? nascimento, DateTime referencia)
    {
        if (nascimento is not { } n) return "Não informada";
        var idade = referencia.Year - n.Year;
        if (DateOnly.FromDateTime(referencia) < n.AddYears(idade)) idade--;
        return idade switch
        {
            < 0 => "Não informada",
            < 18 => "0–17",
            < 30 => "18–29",
            < 45 => "30–44",
            < 60 => "45–59",
            _ => "60+",
        };
    }

    /// <summary>
    /// Desfechos que NÃO recebem pesquisa (códigos de <c>Tipo_Saida</c> do Klinikos): óbito (6),
    /// chegou cadáver (8), evasão (3) e evasão sem atendimento médico (12), boletim extraviado
    /// (9) e baixa administrativa (13, 16).
    ///
    /// <para>Óbito é o que mais importa: convidar a família a avaliar o atendimento de quem
    /// morreu não tem justificativa possível. Evasão não teve atendimento a avaliar.</para>
    /// </summary>
    private static readonly HashSet<string> DesfechosSemPesquisa = ["6", "8", "3", "12", "9", "13", "16"];

    /// <summary>Permanência acima disto é fechamento administrativo tardio, não permanência.</summary>
    private static readonly TimeSpan PermanenciaMaxima = TimeSpan.FromHours(24);

    public async Task<int> ProcessarGatilhoAsync(CancellationToken ct = default)
    {
        var configs = await db.UnidadePesquisaConfigs
            .Where(c => c.EnvioWhatsAppAtivo && c.LinkResponder != null)
            .ToListAsync(ct);
        if (configs.Count == 0) return 0;

        var unidades = await db.Unidades.AsNoTracking()
            .Where(u => configs.Select(c => c.UnidadeId).Contains(u.Id) && u.Cnes != null)
            .ToDictionaryAsync(u => u.Id, u => u.Cnes!, ct);

        // Organização → CNES, uma vez só: o Encounter aponta serviceProvider por id, e é o CNES
        // que casa com a unidade (ADR-0039).
        var orgCnes = (await fhir.BuscarOrganizacoesAsync(ct)).Entry
            .Select(e => e.Resource).OfType<Organization>()
            .Where(o => o.Id is not null)
            .Select(o => new
            {
                Id = o.Id!,
                Cnes = o.Identifier.FirstOrDefault(i => i.System == "https://fhir.saude.gov.br/sid/cnes")?.Value,
            })
            .Where(x => x.Cnes is not null)
            .ToDictionary(x => x.Id, x => x.Cnes!, StringComparer.OrdinalIgnoreCase);

        var enviados = 0;
        foreach (var c in configs)
        {
            if (!unidades.TryGetValue(c.UnidadeId, out var cnes)) continue;

            var ate = DateTime.UtcNow.AddHours(-c.HorasAposAtendimento);
            // Sem marca d'água ainda: começa agora, não no passado. Ligar o gatilho não pode
            // disparar um lote retroativo para quem foi atendido semanas atrás.
            var de = c.UltimoFimProcessadoEm ?? ate.AddMinutes(-1);
            if (ate <= de) continue;

            var bundle = await fhir.BuscarEncerradosAsync(de, ate, ct);
            foreach (var enc in bundle.Entry.Select(e => e.Resource).OfType<Encounter>())
            {
                if (!Elegivel(enc, cnes, orgCnes)) continue;
                if (IdDaReferencia(enc.Subject?.Reference) is not { } pid) continue;
                if (!Guid.TryParse(enc.Id, out var encounterId)) continue;

                try
                {
                    await EnviarAsync(pid, encounterId, ct);
                    enviados++;
                }
                catch (Exception ex) when (ex is ConflitoException or NaoEncontradoException)
                {
                    // Já convidado, sem telefone, sem unidade: são casos esperados e individuais.
                    // Um deles não pode segurar a marca d'água e travar a fila inteira.
                }
            }

            c.UltimoFimProcessadoEm = ate;
            await db.SaveChangesAsync(ct);
        }

        return enviados;
    }

    /// <summary>Regras de quem recebe — ver <see cref="DesfechosSemPesquisa"/> e a guarda de janela.</summary>
    private static bool Elegivel(Encounter enc, string cnes, IReadOnlyDictionary<string, string> orgCnes)
    {
        // A varredura é por janela, não por unidade — o hub devolve todas. Sem este filtro, uma
        // unidade com o gatilho ligado dispararia convite de atendimento das outras.
        if (IdDaReferencia(enc.ServiceProvider?.Reference)?.ToString() is not { } orgId) return false;
        if (!orgCnes.TryGetValue(orgId, out var cnesDoEnc) || cnesDoEnc != cnes) return false;
        if (enc.Period?.Start is null || enc.Period?.End is null) return false;
        if (!DateTimeOffset.TryParse(enc.Period.Start, out var ini)) return false;
        if (!DateTimeOffset.TryParse(enc.Period.End, out var fim)) return false;
        if (fim - ini > PermanenciaMaxima) return false;

        var tipoSaida = enc.Hospitalization?.DischargeDisposition?.Coding
            ?.FirstOrDefault(x => x.System == "urn:klinikos:tiposaida")?.Code;
        return tipoSaida is null || !DesfechosSemPesquisa.Contains(tipoSaida);
    }

    /// <summary>
    /// Cria a pesquisa a partir do atendimento no hub. Passa pelo serviço de atendimentos do
    /// PACIENTE de propósito: é o que garante que ninguém abra pesquisa de encontro alheio.
    /// </summary>
    private async Task<PesquisaSatisfacao> CriarAsync(Guid pacienteId, Guid encounterId, CancellationToken ct)
    {
        var lista = await atendimentos.ObterPorPacienteAsync(pacienteId, ct);
        var a = lista.FirstOrDefault(x => x.Id == encounterId)
            ?? throw new NaoEncontradoException("Atendimento", encounterId);

        // O fim do atendimento é o marco. Sem ele o atendimento não terminou — e não há o que avaliar.
        var fim = (a.Fim ?? a.Inicio)?.UtcDateTime
            ?? throw new ConflitoException(
                "pesquisa.atendimento_sem_data", "Este atendimento ainda não tem data de saída.");

        var p = new PesquisaSatisfacao
        {
            Id = Guid.CreateVersion7(),
            PatientId = pacienteId,
            EncounterId = encounterId,
            // ADR-0039: a unidade é dimensão própria, não deriva do meta.source. Guardar o nome
            // aqui congela o que valia no atendimento — unidade renomeada depois não reescreve o
            // passado de quem já respondeu, e a mensagem cita o lugar onde a pessoa esteve.
            UnidadeNome = a.UnidadeNome,
            UnidadeCnes = a.UnidadeCnes,
            AtendimentoEm = fim,
            ExpiraEm = fim.AddDays(IPesquisasSatisfacaoService.JanelaDias),
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.PesquisasSatisfacao.Add(p);
        return p;
    }

    private string MontarUrl(Guid token)
    {
        var appBase = (configuration["Publico:AppBaseUrl"] ?? "https://app.smsmarica.online").TrimEnd('/');
        return $"{appBase}/pesquisa/{token}";
    }
}
