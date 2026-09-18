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
        Guid? unidadeId, DateOnly? de, DateOnly? ate, bool forcar = false, CancellationToken ct = default);

    /// <summary>Enfileira o lote. Idempotente: quem já tem comunicação de confirmação não entra de novo.</summary>
    /// <param name="forcar">Ignora as chaves de unidade e de procedimento — o lote passa a ser
    /// decisão de quem clica, não do cadastro. Exige unidade escolhida. As regras de LGPD (contato
    /// negado, número não verificado → desafio) continuam valendo: elas não são chave de operação.</param>
    Task<PreviaLoteConfirmacaoDto> DispararAsync(
        Guid? unidadeId, DateOnly? de, DateOnly? ate, bool forcar = false, CancellationToken ct = default);
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
        Guid? unidadeId, DateOnly? de, DateOnly? ate, bool forcar = false, CancellationToken ct = default) =>
        MontarAsync(unidadeId, de, ate, forcar, disparar: false, ct);

    public Task<PreviaLoteConfirmacaoDto> DispararAsync(
        Guid? unidadeId, DateOnly? de, DateOnly? ate, bool forcar = false, CancellationToken ct = default) =>
        MontarAsync(unidadeId, de, ate, forcar, disparar: true, ct);

    private async Task<PreviaLoteConfirmacaoDto> MontarAsync(
        Guid? unidadeId, DateOnly? de, DateOnly? ate, bool forcar, bool disparar, CancellationToken ct)
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
                && s.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente)
            .Include(s => s.ExameImagem)
            .OrderBy(s => s.DataAgendada)
            .ToListAsync(ct);

        var jaTemComunicacao = await db.ComunicacoesPaciente.AsNoTracking()
            .Where(c => c.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento && c.SolicitacaoId != null)
            .Select(c => c.SolicitacaoId!.Value)
            .ToHashSetAsync(ct);

        var elegiveis = new List<Solicitacao>();
        int foraProcedimento = 0, foraJaAvisado = 0, foraForaDoSisreg = 0;

        foreach (var s in candidatos)
        {
            if (jaTemComunicacao.Contains(s.Id)) { foraJaAvisado++; continue; }
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

            var quantos = 0;
            foreach (var s in elegiveis)
            {
                await comunicacoes.EnfileirarAsync(s, FinalidadeComunicacao.ConfirmacaoAgendamento, ct);
                quantos++;
            }

            // Carimba quem mandou: lote é ato de gente, e a fila mostra isso no detalhe.
            foreach (var entrada in db.ChangeTracker.Entries<ComunicacaoPaciente>()
                         .Where(e => e.State == EntityState.Added))
            {
                entrada.Entity.Origem = OrigemComunicacao.Manual;
                entrada.Entity.EnviadoPor = usuarioAtual.UsuarioId;
            }

            await db.SaveChangesAsync(ct);
            enfileiradas = quantos;
            logger.LogInformation(
                "Lote de confirmação: {Qtd} agendamento(s) enfileirado(s) (unidade {Unidade}, {De} a {Ate}, "
                + "forcar={Forcar}) por {Usuario}.",
                quantos, unidadeId, diaInicial, diaFinal, forcar, usuarioAtual.UsuarioId);
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
            aviso);
    }
}
