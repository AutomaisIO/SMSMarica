using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Ser.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Ser;

namespace SMSMarica.Core.Ser;

/// <summary>
/// Busca na NOSSA base espelhada do SER — ADR-0042.
///
/// <para><b>Não fala com o SER.</b> A tela do operador lê o espelho, que é atualizado pelo motor
/// de varredura. Isso é o ponto: consultar o SER ao vivo custaria ~0,7 s por busca, exigiria
/// sessão ativa (que é única por operador e derrubaria o humano logado) e ficaria refém do teto
/// de 100 registros da tela deles.</para>
/// </summary>
public interface ISerConsultaService
{
    Task<SerBuscaResultadoDto> BuscarAsync(SerBuscaFiltroDto filtro, CancellationToken cancellationToken);
    Task<SerSolicitacaoDetalheDto> ObterAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<SerResumoSituacaoDto>> ResumoPorSituacaoAsync(CancellationToken cancellationToken);
}

public sealed class SerConsultaService(SmsMaisDbContext db) : ISerConsultaService
{
    private const int TamanhoMaximo = 200;

    public async Task<SerBuscaResultadoDto> BuscarAsync(
        SerBuscaFiltroDto filtro, CancellationToken cancellationToken)
    {
        var consulta = db.SerSolicitacoes.AsNoTracking().Where(x => x.ExcluidoEm == null);

        if (filtro.Situacao is { } situacao) consulta = consulta.Where(x => x.Situacao == situacao);
        if (filtro.Tipo is { } tipo) consulta = consulta.Where(x => x.Tipo == tipo);
        if (filtro.DataSolicitacaoInicio is { } di) consulta = consulta.Where(x => x.DataSolicitacao >= di);
        if (filtro.DataSolicitacaoFim is { } df) consulta = consulta.Where(x => x.DataSolicitacao <= df);
        if (filtro.MudouDesde is { } desde) consulta = consulta.Where(x => x.SituacaoMudouEm >= desde);

        if (!string.IsNullOrWhiteSpace(filtro.Termo))
        {
            var termo = filtro.Termo.Trim();
            var digitos = new string(termo.Where(char.IsDigit).ToArray());

            consulta = consulta.Where(x =>
                EF.Functions.ILike(x.PacienteNome, $"%{termo}%")
                || EF.Functions.ILike(x.Recurso, $"%{termo}%")
                || x.IdSer == termo
                || (digitos.Length > 0 && (x.Cpf == digitos || x.Cns == digitos)));
        }

        var total = await consulta.CountAsync(cancellationToken);

        var pagina = Math.Max(filtro.Pagina, 1);
        var tamanho = Math.Clamp(filtro.Tamanho, 1, TamanhoMaximo);

        var itens = await consulta
            // Mais antigas primeiro: numa fila, quem espera há mais tempo é quem importa.
            .OrderBy(x => x.DataSolicitacao)
            .ThenBy(x => x.IdSer)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .ToListAsync(cancellationToken);

        return new SerBuscaResultadoDto(
            itens.Select(ParaLista).ToList(), total, pagina, tamanho);
    }

    public async Task<SerSolicitacaoDetalheDto> ObterAsync(Guid id, CancellationToken cancellationToken)
    {
        var s = await db.SerSolicitacoes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException("Solicitação do SER", id);

        var eventos = await db.SerEventos
            .AsNoTracking()
            .Where(e => e.SerSolicitacaoId == id)
            // Mais recente primeiro — é como o SER mostra e como o operador lê.
            .OrderByDescending(e => e.DataEvento)
            .Select(e => new SerEventoDto(
                e.Id, e.DataEvento, e.Evento, e.EstadoAnterior, e.EstadoAtual,
                e.CentralRegulacao, e.UnidadeExecutora, e.Usuario, e.LotacaoEvento, e.Ip, e.Observacao))
            .ToListAsync(cancellationToken);

        return new SerSolicitacaoDetalheDto(
            ParaLista(s),
            s.NomeMae, s.Sexo, s.DataNascimento, s.Etnia, s.Cep, s.Uf, s.MunicipioPaciente,
            s.Bairro, s.TipoLogradouro, s.Logradouro, s.Numero, s.Complemento,
            s.TelefoneResidencial, s.TelefoneWhatsapp, s.TelefoneContato,
            eventos);
    }

    public async Task<IReadOnlyList<SerResumoSituacaoDto>> ResumoPorSituacaoAsync(
        CancellationToken cancellationToken)
    {
        // Projeta para tipo ANÔNIMO e ordena em memória. Ordenar por uma propriedade do DTO
        // (`OrderByDescending(x => x.Quantidade)`) depois de projetar num construtor de record
        // NÃO é traduzível pelo EF — quebrava com InvalidOperationException em runtime, não em
        // compilação (ERRO-T8E5Q9, 06/08/2026). São no máximo 7 linhas (uma por situação),
        // então ordenar no cliente não custa nada.
        var contagens = await db.SerSolicitacoes
            .AsNoTracking()
            .Where(x => x.ExcluidoEm == null)
            .GroupBy(x => x.Situacao)
            .Select(g => new { Situacao = g.Key, Quantidade = g.Count() })
            .ToListAsync(cancellationToken);

        return contagens
            .OrderByDescending(x => x.Quantidade)
            .Select(x => new SerResumoSituacaoDto(x.Situacao, x.Quantidade))
            .ToList();
    }

    private static SerSolicitacaoListaDto ParaLista(SerSolicitacao s) => new(
        s.Id, s.IdSer, s.Tipo, s.Recurso, s.DataSolicitacao, s.PacienteNome, s.PacienteId, s.IdadeTexto,
        s.Cpf, s.Cns, s.Cid, s.SolicitanteNome, s.MunicipioSolicitante, s.AgendadoParaTexto,
        s.Situacao, s.SituacaoAnterior, s.SituacaoMudouEm, s.SincronizadoEm, s.HistoricoLidoEm,
        s.EventosCount, s.HistoricoIndisponivel,
        s.DataSolicitacao is { } d
            ? DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - d.DayNumber
            : null);
}
