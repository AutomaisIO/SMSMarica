using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Notificacoes.Comunicacao.Dtos;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Notificacoes.Comunicacao;

/// <summary>
/// Consulta da tela de gestão das comunicações ao paciente (painel): lista paginada com
/// status de envio/entrega/leitura/visualização, resposta do paciente e motivo; detalhe com
/// linha do tempo; reenvio manual (re-enfileira para o worker, que gera magic link novo).
/// </summary>
public interface IComunicacaoGestaoService
{
    Task<PaginaComunicacoesDto> ListarAsync(ComunicacaoFiltroDto filtro, CancellationToken ct = default);
    Task<ComunicacaoDetalheDto> ObterAsync(Guid id, CancellationToken ct = default);
    Task ReenviarAsync(Guid id, CancellationToken ct = default);

    /// <summary>Resumo diário da mensageria (Bernardo: "medir a qualidade das entregas via zap").
    /// Dias de Brasília, inclusivos; sem período, últimos 30 dias.</summary>
    Task<ResumoDiarioMensageriaDto> ResumoDiarioAsync(
        DateOnly? de, DateOnly? ate, string? finalidade, Guid? unidadeId, CancellationToken ct = default);
}

public sealed class ComunicacaoGestaoService(
    SmsMaisDbContext db,
    IPacienteResolver pacienteResolver) : IComunicacaoGestaoService
{
    public async Task<PaginaComunicacoesDto> ListarAsync(ComunicacaoFiltroDto filtro, CancellationToken ct = default)
    {
        var query = db.ComunicacoesPaciente.AsNoTracking()
            .Include(n => n.Solicitacao!).ThenInclude(s => s.ExameImagem!).ThenInclude(e => e.TipoExame)
            .Include(n => n.Solicitacao!).ThenInclude(s => s.UnidadeExecutante)
            .AsQueryable();

        if (Enum.TryParse<StatusComunicacao>(filtro.Status, ignoreCase: true, out var st))
            query = query.Where(n => n.Status == st);
        if (Enum.TryParse<FinalidadeComunicacao>(filtro.Finalidade, ignoreCase: true, out var fin))
            query = query.Where(n => n.Finalidade == fin);
        if (Enum.TryParse<StatusConfirmacaoAgendamento>(filtro.Confirmacao, ignoreCase: true, out var conf))
            query = query.Where(n => n.Solicitacao != null && n.Solicitacao.StatusConfirmacao == conf);
        if (filtro.De is { } de) query = query.Where(n => n.CriadoEm >= de);
        if (filtro.Ate is { } ate) query = query.Where(n => n.CriadoEm < ate.AddDays(1));
        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var t = filtro.Texto.Trim();
            query = query.Where(n => n.Solicitacao != null
                && ((n.Solicitacao.ExameImagem != null && n.Solicitacao.ExameImagem.AccessionNumber.Contains(t))
                    || (n.Solicitacao.CodigoSolicitacao != null && n.Solicitacao.CodigoSolicitacao.Contains(t))
                    || (n.Telefone != null && n.Telefone.Contains(t))));
        }

        var total = await query.CountAsync(ct);
        var pagina = Math.Max(1, filtro.Pagina);
        var tamanho = Math.Clamp(filtro.Tamanho, 1, 200);

        var linhas = await query
            .OrderByDescending(n => n.CriadoEm)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .ToListAsync(ct);

        var nomes = await pacienteResolver.ResolverManyAsync(linhas.Select(n => n.PacienteId), ct);

        var itens = linhas.Select(n => Mapear(
            n, nomes.TryGetValue(n.PacienteId, out var r) ? r.Nome : null)).ToList();

        return new PaginaComunicacoesDto(itens, total, pagina, tamanho);
    }

    public async Task<ComunicacaoDetalheDto> ObterAsync(Guid id, CancellationToken ct = default)
    {
        var n = await db.ComunicacoesPaciente.AsNoTracking()
            .Include(x => x.Solicitacao!).ThenInclude(s => s.ExameImagem!).ThenInclude(e => e.TipoExame)
            .Include(x => x.Solicitacao!).ThenInclude(s => s.UnidadeExecutante)
            .Include(x => x.MensagemWhatsApp)
            .Include(x => x.LoginLink)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NaoEncontradoException("comunicacao.nao_encontrada", "Comunicação não encontrada.");

        var resumo = Mapear(n, (await pacienteResolver.ResolverAsync(n.PacienteId, ct))?.Nome);

        return new ComunicacaoDetalheDto(
            resumo,
            n.UltimaTentativaEm,
            n.ProximaTentativaEm,
            n.MensagemWhatsApp?.Conteudo,
            n.MensagemWhatsApp?.Status.ToString(),
            n.MensagemWhatsApp?.ErroMeta,
            n.LoginLink?.ExpiraEm,
            n.LoginLink?.UsadoEm,
            n.LoginLink?.UsadoIp);
    }

    public async Task ReenviarAsync(Guid id, CancellationToken ct = default)
    {
        var n = await db.ComunicacoesPaciente
            .Include(x => x.Solicitacao)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NaoEncontradoException("comunicacao.nao_encontrada", "Comunicação não encontrada.");

        if (n.Status == StatusComunicacao.Pendente && n.ProximaTentativaEm is not null)
            throw new ConflitoException("comunicacao.ja_na_fila", "Esta comunicação já está na fila de envio.");
        if (n.Solicitacao is null || n.Solicitacao.ExcluidoEm is not null)
            throw new ConflitoException("comunicacao.sem_solicitacao", "A solicitação desta comunicação não existe mais.");
        if (n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento)
        {
            if (n.Solicitacao.DataAgendada is not { } da || da <= DateTime.UtcNow)
                throw new ConflitoException("comunicacao.exame_passado", "O exame já aconteceu — não faz sentido reenviar.");
            if (n.Solicitacao.StatusConfirmacao != StatusConfirmacaoAgendamento.Pendente)
                throw new ConflitoException("comunicacao.ja_respondida", "O paciente já respondeu este agendamento.");
        }

        n.Status = StatusComunicacao.Pendente;
        n.MotivoFalha = null;
        n.ProximaTentativaEm = DateTime.UtcNow;
        n.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<ResumoDiarioMensageriaDto> ResumoDiarioAsync(
        DateOnly? de, DateOnly? ate, string? finalidade, Guid? unidadeId, CancellationToken ct = default)
    {
        var hoje = DateOnly.FromDateTime(Common.Tempo.FusoBrasilia.ParaExibicao(DateTime.UtcNow));
        var fim = ate ?? hoje;
        var inicio = de ?? fim.AddDays(-29);
        if (fim < inicio) (inicio, fim) = (fim, inicio);
        if (fim.DayNumber - inicio.DayNumber > 400)
            throw new ValidacaoException("periodo", "O período não pode exceder 400 dias.");

        var inicioUtc = Common.Tempo.FusoBrasilia.DeBrasiliaParaUtc(inicio.ToDateTime(TimeOnly.MinValue));
        var limiteUtc = Common.Tempo.FusoBrasilia.DeBrasiliaParaUtc(fim.AddDays(1).ToDateTime(TimeOnly.MinValue));

        // Coorte pelo dia de entrada + as enviadas no período (para a coluna "enviadas no dia").
        var query = db.ComunicacoesPaciente.AsNoTracking()
            .Where(c => (c.CriadoEm >= inicioUtc && c.CriadoEm < limiteUtc)
                || (c.EnviadoEm != null && c.EnviadoEm >= inicioUtc && c.EnviadoEm < limiteUtc));
        if (Enum.TryParse<FinalidadeComunicacao>(finalidade, ignoreCase: true, out var fin))
            query = query.Where(c => c.Finalidade == fin);
        if (unidadeId is { } u)
            query = query.Where(c => c.Solicitacao != null && c.Solicitacao.UnidadeExecutanteId == u);

        var linhas = await query
            .Select(c => new
            {
                c.CriadoEm, c.EnviadoEm, c.EntregueEm, c.LidoEm, c.VisualizadoEm, c.Status, c.MotivoFalha,
                c.Finalidade, c.ProximaTentativaEm,
                ErroMeta = c.MensagemWhatsApp != null ? c.MensagemWhatsApp.ErroMeta : null,
                Resposta = c.Solicitacao != null ? (StatusConfirmacaoAgendamento?)c.Solicitacao.StatusConfirmacao : null,
                Unidade = c.Solicitacao != null && c.Solicitacao.UnidadeExecutante != null ? c.Solicitacao.UnidadeExecutante.Nome : null,
            })
            .ToListAsync(ct);

        static DateOnly Dia(DateTime utc) => DateOnly.FromDateTime(Common.Tempo.FusoBrasilia.ParaExibicao(utc));

        var dias = new SortedDictionary<DateOnly, int[]>();
        int[] Slot(DateOnly d) { if (!dias.TryGetValue(d, out var s)) dias[d] = s = new int[16]; return s; }
        var falhasPorErro = new Dictionary<string, int>();
        var porFinalidade = new Dictionary<string, int>();
        var porUnidade = new Dictionary<string, int>();

        foreach (var l in linhas)
        {
            if (l.EnviadoEm is { } env && env >= inicioUtc && env < limiteUtc)
                Slot(Dia(env))[1]++; // EnviadasNoDia
            if (l.CriadoEm < inicioUtc || l.CriadoEm >= limiteUtc) continue;

            var s = Slot(Dia(l.CriadoEm));
            s[0]++;
            if (l.EnviadoEm is not null) s[2]++;
            if (l.EntregueEm is not null) s[3]++;
            if (l.LidoEm is not null) s[4]++;
            if (l.VisualizadoEm is not null) s[5]++;
            switch (l.Status)
            {
                case StatusComunicacao.Falha:
                    s[6]++;
                    var erro = RotuloErro(l.ErroMeta ?? l.MotivoFalha);
                    falhasPorErro[erro] = falhasPorErro.GetValueOrDefault(erro) + 1;
                    break;
                case StatusComunicacao.Pendente when l.ProximaTentativaEm is not null: s[7]++; break;
                case StatusComunicacao.AguardandoVerificacaoCadastral: s[8]++; break;
                case StatusComunicacao.AguardandoCorrecaoContato: s[9]++; break;
                case StatusComunicacao.SemTelefoneValido: s[10]++; break;
                case StatusComunicacao.AguardandoTelefoneVerificado: s[11]++; break;
                case StatusComunicacao.SubstituidaPorAtendente: s[12]++; break;
            }
            if (l.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento)
            {
                switch (l.Resposta)
                {
                    case StatusConfirmacaoAgendamento.Confirmada: s[13]++; break;
                    case StatusConfirmacaoAgendamento.Cancelada: s[14]++; break;
                    default: if (l.EnviadoEm is not null) s[15]++; break;
                }
            }
            porFinalidade[l.Finalidade.ToString()] = porFinalidade.GetValueOrDefault(l.Finalidade.ToString()) + 1;
            var un = l.Unidade ?? "(sem unidade)";
            porUnidade[un] = porUnidade.GetValueOrDefault(un) + 1;
        }

        var lista = dias
            .Where(kv => kv.Key >= inicio && kv.Key <= fim)
            .Select(kv => new DiaMensageriaDto(kv.Key,
                kv.Value[0], kv.Value[1], kv.Value[2], kv.Value[3], kv.Value[4], kv.Value[5], kv.Value[6],
                kv.Value[7], kv.Value[8], kv.Value[9], kv.Value[10], kv.Value[11], kv.Value[12],
                kv.Value[13], kv.Value[14], kv.Value[15]))
            .ToList();

        int Soma(Func<DiaMensageriaDto, int> f) => lista.Sum(f);
        var enfileiradas = Soma(d => d.Enfileiradas);
        var enviadas = Soma(d => d.Enviadas);
        var entregues = Soma(d => d.Entregues);
        var lidas = Soma(d => d.Lidas);
        var respondidas = Soma(d => d.Confirmadas) + Soma(d => d.Canceladas);
        var totais = new TotaisMensageriaDto(
            enfileiradas, enviadas, entregues, lidas, Soma(d => d.Visualizadas), Soma(d => d.Falhas),
            Soma(d => d.AguardandoIdentificacao) + Soma(d => d.NumeroNegado) + Soma(d => d.SemTelefone) + Soma(d => d.AguardandoVerificado),
            Soma(d => d.SubstituidasPorAtendente), Soma(d => d.Confirmadas), Soma(d => d.Canceladas), Soma(d => d.SemResposta),
            enviadas > 0 ? Math.Round(100.0 * entregues / enviadas, 1) : 0,
            enviadas > 0 ? Math.Round(100.0 * lidas / enviadas, 1) : 0,
            enviadas > 0 ? Math.Round(100.0 * respondidas / enviadas, 1) : 0);

        static IReadOnlyList<ErroMensageriaDto> Ordenar(Dictionary<string, int> d) =>
            [.. d.OrderByDescending(kv => kv.Value).Select(kv => new ErroMensageriaDto(kv.Key, kv.Value))];

        return new ResumoDiarioMensageriaDto(inicio, fim, totais, lista,
            Ordenar(falhasPorErro), Ordenar(porFinalidade), Ordenar(porUnidade));
    }

    /// <summary>Agrupa falhas pelo código Meta "(131026) ..." quando existe; senão pelo motivo curto.</summary>
    private static string RotuloErro(string? erro)
    {
        if (string.IsNullOrWhiteSpace(erro)) return "(sem motivo)";
        erro = erro.Trim();
        var fecha = erro.IndexOf(')');
        if (erro.StartsWith('(') && fecha > 0)
        {
            var resto = erro[(fecha + 1)..].Trim().TrimStart('—', '-', ' ');
            var corte = resto.IndexOf('—');
            if (corte > 0) resto = resto[..corte].Trim();
            return $"{erro[..(fecha + 1)]} {resto}".Trim();
        }
        return erro.Length <= 80 ? erro : erro[..80];
    }

    private static ComunicacaoResumoDto Mapear(ComunicacaoPaciente n, string? pacienteNome)
    {
        var s = n.Solicitacao;
        return new ComunicacaoResumoDto(
            n.Id,
            n.Finalidade.ToString(),
            n.SolicitacaoId,
            s?.ExameImagem?.AccessionNumber,
            s?.CodigoSolicitacao,
            n.PacienteId,
            pacienteNome,
            s?.ExameImagem?.TipoExame?.Nome,
            s?.UnidadeExecutante?.Nome,
            s?.DataAgendada,
            n.Telefone,
            n.Status.ToString(),
            n.MotivoFalha,
            n.Tentativas,
            n.EnviadoEm,
            n.EntregueEm,
            n.LidoEm,
            n.VisualizadoEm,
            (s?.StatusConfirmacao ?? StatusConfirmacaoAgendamento.Pendente).ToString(),
            s?.ConfirmadoEm,
            s?.ConfirmadoCanal,
            s?.MotivoCancelamentoPaciente,
            n.CriadoEm);
    }
}
