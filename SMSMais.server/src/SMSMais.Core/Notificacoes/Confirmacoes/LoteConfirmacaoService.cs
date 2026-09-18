using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Identidade;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.Confirmacoes.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Notificacoes.Confirmacoes;

/// <summary>
/// Disparo em LOTE da confirmação para agendamentos que já estão no sistema.
///
/// <para>Existe porque o aviso nasce na IMPORTAÇÃO: quando a unidade liga a chave, o que já foi
/// importado antes fica para trás — e é justamente a agenda dos próximos dias, que é o que
/// interessa avisar. Sem isto a única saída seria esperar a próxima varredura e perder o estoque.</para>
///
/// <para><b>Mesma régua da importação</b>, nada afrouxado: só SISREG, unidade com a chave ligada,
/// procedimento marcado para avisar, agendamento no futuro e paciente que ainda não respondeu.
/// Só ENFILEIRA — quem envia é o worker, respeitando a janela de horário e a vazão.</para>
/// </summary>
public interface ILoteConfirmacaoService
{
    /// <summary>Quantos seriam avisados (e quantos ficam de fora, por quê). Não muda nada.
    /// <paramref name="de"/> e <paramref name="ate"/> são DIAS de Brasília, inclusivos — é assim que
    /// se decide "não avise o de amanhã, comece na segunda".</summary>
    Task<PreviaLoteConfirmacaoDto> PreverAsync(
        Guid? unidadeId, DateOnly? de, DateOnly? ate, bool forcar = false,
        bool incluirJaAvisados = false, bool incluirJaConfirmados = false, CancellationToken ct = default);

    /// <summary>Enfileira o lote. Idempotente: quem já tem comunicação de confirmação não entra de novo.</summary>
    /// <param name="forcar">Ignora as chaves de unidade e de procedimento — o lote passa a ser
    /// decisão de quem clica, não do cadastro. Exige unidade escolhida. As regras de LGPD (contato
    /// negado, número não verificado → desafio) continuam valendo: elas não são chave de operação.</param>
    /// <param name="ignorarJanela">Este lote sai AGORA, mesmo fora do horário de envio. Vale só
    /// para as mensagens deste disparo — a janela continua de pé para todo o resto.</param>
    /// <param name="incluirJaAvisados">Quem já recebeu a confirmação entra de novo: a comunicação é
    /// REARMADA (links antigos revogados, recibos zerados, envio novo) em vez de pulada.</param>
    /// <param name="incluirJaConfirmados">Quem já confirmou também recebe de novo — a resposta
    /// anterior volta a Pendente (fica na trilha de contato) e o paciente reconfirma.</param>
    Task<PreviaLoteConfirmacaoDto> DispararAsync(
        Guid? unidadeId, DateOnly? de, DateOnly? ate, bool forcar = false, bool ignorarJanela = false,
        bool incluirJaAvisados = false, bool incluirJaConfirmados = false, CancellationToken ct = default);
}

public sealed class LoteConfirmacaoService(
    SmsMaisDbContext db,
    IComunicacaoPacienteService comunicacoes,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<LoteConfirmacaoService> logger) : ILoteConfirmacaoService
{
    /// <summary>Teto por disparo: acima disso é decisão de gente, não de um clique.</summary>
    public const int MaximoPorLote = 2000;

    public Task<PreviaLoteConfirmacaoDto> PreverAsync(
        Guid? unidadeId, DateOnly? de, DateOnly? ate, bool forcar = false,
        bool incluirJaAvisados = false, bool incluirJaConfirmados = false, CancellationToken ct = default) =>
        MontarAsync(unidadeId, de, ate, forcar, disparar: false, ct, false, incluirJaAvisados, incluirJaConfirmados);

    public Task<PreviaLoteConfirmacaoDto> DispararAsync(
        Guid? unidadeId, DateOnly? de, DateOnly? ate, bool forcar = false, bool ignorarJanela = false,
        bool incluirJaAvisados = false, bool incluirJaConfirmados = false, CancellationToken ct = default) =>
        MontarAsync(unidadeId, de, ate, forcar, disparar: true, ct, ignorarJanela, incluirJaAvisados, incluirJaConfirmados);

    private async Task<PreviaLoteConfirmacaoDto> MontarAsync(
        Guid? unidadeId, DateOnly? de, DateOnly? ate, bool forcar, bool disparar, CancellationToken ct,
        bool ignorarJanela = false, bool incluirJaAvisados = false, bool incluirJaConfirmados = false)
    {
        // Forçar é o modo "eu sei o que estou fazendo": ignora as chaves, mas exige alvo estreito —
        // sem unidade escolhida seria um disparo para a rede inteira num clique.
        if (forcar && unidadeId is null)
            throw new ValidacaoException(
                "lote.forcar_exige_unidade", "Para ignorar as chaves, escolha a unidade do lote.");

        var agora = DateTime.UtcNow;
        var hoje = DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(agora));
        var diaInicial = de ?? hoje;
        var diaFinal = ate ?? diaInicial.AddDays(9);

        if (diaFinal < diaInicial)
            throw new ValidacaoException("lote.periodo_invalido", "A data final não pode ser anterior à inicial.");
        if (diaInicial < hoje)
            throw new ValidacaoException("lote.periodo_passado", "O lote só avisa agendamento que ainda vai acontecer.");
        if (diaFinal.DayNumber - diaInicial.DayNumber > 60)
            throw new ValidacaoException("lote.periodo_longo", "O período não pode passar de 60 dias.");

        // Dias de BRASÍLIA (inclusivos) → instantes UTC; e nunca antes de agora.
        var inicio = FusoBrasilia.DeBrasiliaParaUtc(diaInicial.ToDateTime(TimeOnly.MinValue));
        if (inicio < agora) inicio = agora;
        var limite = FusoBrasilia.DeBrasiliaParaUtc(diaFinal.AddDays(1).ToDateTime(TimeOnly.MinValue));

        // Unidades do lote: as com a chave ligada (a mesma da importação) — ou a escolhida, quando
        // se força (a chave desligada continua valendo para o automático; o lote é que é manual).
        var unidadesLigadas = forcar
            ? [unidadeId!.Value]
            : await db.SisregVarreduraAgendas.AsNoTracking()
                .Where(a => a.EnviarConfirmacao && (unidadeId == null || a.UnidadeId == unidadeId))
                .Select(a => a.UnidadeId)
                .ToListAsync(ct);
        if (unidadesLigadas.Count == 0)
        {
            return new PreviaLoteConfirmacaoDto(
                0, 0, 0, 0, 0, [], disparar ? 0 : null,
                "Nenhuma unidade com o aviso ligado neste filtro — ligue a chave da unidade primeiro.");
        }

        // Procedimentos marcados para avisar, por unidade (código do SISREG).
        var procs = await db.SisregProcedimentosProfissional.AsNoTracking()
            .Where(p => p.Profissional != null && unidadesLigadas.Contains(p.Profissional.UnidadeId) && p.EnviarConfirmacao)
            .Select(p => new { p.Profissional!.UnidadeId, p.Codigo })
            .Distinct()
            .ToListAsync(ct);
        var codigosPorUnidade = procs
            .GroupBy(p => p.UnidadeId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Codigo).ToHashSet(StringComparer.Ordinal));

        // Candidatos: agendamento futuro dentro da janela, ainda sem resposta do paciente.
        var candidatos = await db.Solicitacoes
            .Where(s => s.ExcluidoEm == null
                && s.Status != StatusSolicitacao.Cancelada
                && unidadesLigadas.Contains(s.UnidadeExecutanteId)
                && s.DataAgendada > inicio && s.DataAgendada < limite
                && (s.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente
                    || (incluirJaConfirmados && s.StatusConfirmacao == StatusConfirmacaoAgendamento.Confirmada)))
            .Include(s => s.ExameImagem)
            .OrderBy(s => s.DataAgendada)
            .ToListAsync(ct);

        var candidatoIds = candidatos.Select(c => c.Id).ToList();
        var jaTemComunicacao = await db.ComunicacoesPaciente.AsNoTracking()
            .Where(c => c.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                && c.SolicitacaoId != null && candidatoIds.Contains(c.SolicitacaoId.Value))
            .Select(c => c.SolicitacaoId!.Value)
            .ToHashSetAsync(ct);

        var elegiveis = new List<Solicitacao>();
        // Reenvios: quem entra de novo por decisão de quem dispara (já avisado e/ou já confirmado).
        var reenvios = new List<Solicitacao>();
        int foraProcedimento = 0, foraJaAvisado = 0, foraForaDoSisreg = 0, reenviosAvisados = 0, reenviosConfirmados = 0;

        foreach (var s in candidatos)
        {
            var jaConfirmou = s.StatusConfirmacao == StatusConfirmacaoAgendamento.Confirmada;
            var jaAvisado = jaTemComunicacao.Contains(s.Id);
            if (jaAvisado && !incluirJaAvisados && !jaConfirmou) { foraJaAvisado++; continue; }
            if (!OrigemAgendamento.EhDoSisreg(s)) { foraForaDoSisreg++; continue; }

            if (!forcar)
            {
                var codigo = s.ProcedimentoCodigoSisreg?.Trim();
                var ligados = codigosPorUnidade.GetValueOrDefault(s.UnidadeExecutanteId);
                if (string.IsNullOrEmpty(codigo) || ligados is null || !ligados.Contains(codigo))
                {
                    foraProcedimento++;
                    continue;
                }
            }

            if (jaConfirmou) { reenviosConfirmados++; reenvios.Add(s); }
            else if (jaAvisado) { reenviosAvisados++; reenvios.Add(s); }
            elegiveis.Add(s);
        }

        var porDia = elegiveis
            .GroupBy(s => DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(s.DataAgendada!.Value)))
            .OrderBy(g => g.Key)
            .Select(g => new LoteConfirmacaoPorDiaDto(
                g.Key,
                g.Count(),
                g.Count(x => x.Categoria == CategoriaSolicitacao.Consulta),
                g.Count(x => x.Categoria != CategoriaSolicitacao.Consulta)))
            .ToList();

        int? enfileiradas = null;
        string? aviso = null;

        if (disparar)
        {
            if (elegiveis.Count > MaximoPorLote)
            {
                throw new ConflitoException(
                    "lote.acima_do_teto",
                    $"O lote tem {elegiveis.Count} agendamentos e o teto por disparo é {MaximoPorLote}. "
                    + "Reduza os dias à frente e dispare em partes.");
            }

            var agoraDisparo = DateTime.UtcNow;
            var reenvioIds = reenvios.Select(r => r.Id).ToHashSet();
            var comunicacoesExistentes = reenvioIds.Count == 0
                ? []
                : await db.ComunicacoesPaciente
                    .Where(c => c.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                        && c.SolicitacaoId != null && reenvioIds.Contains(c.SolicitacaoId.Value))
                    .ToListAsync(ct);

            var quantos = 0;
            foreach (var s in elegiveis)
            {
                if (reenvioIds.Contains(s.Id))
                {
                    // Reenvio por decisão de quem dispara. Quem já confirmou volta a Pendente (a
                    // resposta anterior fica na trilha) — senão o worker mata o envio como "já
                    // respondeu". Links antigos são revogados: podem estar com a pessoa errada.
                    if (s.StatusConfirmacao == StatusConfirmacaoAgendamento.Confirmada)
                    {
                        db.ContatosRegistro.Add(new ContatoRegistro
                        {
                            Id = Guid.CreateVersion7(),
                            SolicitacaoId = s.Id,
                            PacienteId = s.PacienteId,
                            Meio = MeioContato.WhatsApp,
                            Resultado = ResultadoContato.Outro,
                            Observacao = $"Reconfirmação pedida em lote (estava confirmada em {s.ConfirmadoEm:dd/MM/yyyy HH:mm} via {s.ConfirmadoCanal}).",
                            CriadoEm = agoraDisparo,
                            CriadoPor = usuarioAtual.UsuarioId,
                        });
                        s.StatusConfirmacao = StatusConfirmacaoAgendamento.Pendente;
                        s.ConfirmadoEm = null;
                        s.ConfirmadoCanal = null;
                        s.AtualizadoEm = agoraDisparo;
                        s.AtualizadoPor = usuarioAtual.UsuarioId;
                    }
                    var existente = comunicacoesExistentes.FirstOrDefault(c => c.SolicitacaoId == s.Id);
                    if (existente is not null)
                    {
                        await comunicacoes.RevogarAcessosAsync(s.Id, agoraDisparo, ct);
                        ComunicacaoPacienteService.RearmarParaNovoEnvio(existente);
                        existente.Origem = OrigemComunicacao.Manual;
                        existente.EnviadoPor = usuarioAtual.UsuarioId;
                        existente.IgnorarJanelaHorario = ignorarJanela;
                        quantos++;
                        continue;
                    }
                }
                await comunicacoes.EnfileirarAsync(s, FinalidadeComunicacao.ConfirmacaoAgendamento, ct);
                quantos++;
            }

            // Carimba quem mandou: lote é ato de gente, e a fila mostra isso no detalhe.
            foreach (var entrada in db.ChangeTracker.Entries<ComunicacaoPaciente>()
                         .Where(e => e.State == EntityState.Added))
            {
                entrada.Entity.Origem = OrigemComunicacao.Manual;
                entrada.Entity.EnviadoPor = usuarioAtual.UsuarioId;
                // "Sai agora" é decisão de quem dispara, e vale SÓ para este lote: a janela protege
                // o paciente de receber de madrugada, mas quem está olhando a operação pode decidir
                // que este envio não espera até as 8h.
                entrada.Entity.IgnorarJanelaHorario = ignorarJanela;
            }

            await db.SaveChangesAsync(ct);
            enfileiradas = quantos;
            logger.LogInformation(
                "Lote de confirmação: {Qtd} agendamento(s) enfileirado(s) (unidade {Unidade}, {De} a {Ate}, "
                + "forcar={Forcar}, ignorarJanela={IgnorarJanela}) por {Usuario}.",
                quantos, unidadeId, diaInicial, diaFinal, forcar, ignorarJanela, usuarioAtual.UsuarioId);
        }
        else if (elegiveis.Count > MaximoPorLote)
        {
            aviso = $"O lote tem {elegiveis.Count} agendamentos — acima do teto de {MaximoPorLote} por disparo. "
                + "Reduza os dias à frente e dispare em partes.";
        }

        return new PreviaLoteConfirmacaoDto(
            elegiveis.Count,
            candidatos.Count,
            foraProcedimento,
            foraJaAvisado,
            foraForaDoSisreg,
            porDia,
            enfileiradas,
            aviso,
            reenviosAvisados,
            reenviosConfirmados);
    }
}
