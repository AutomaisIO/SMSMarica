using System.Diagnostics;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Integracoes.SerWeb.Varredura;
using SMSMais.Core.Ser.Dtos;

namespace SMSMais.Core.Ser;

/// <summary>
/// Consulta DIRETA ao SER — a bancada de testes da integração (ADR-0042).
///
/// <para><b>Por que existe:</b> o motor é web scraping de uma tela JSF. "Compila" não diz nada
/// sobre "consegue logar, ativar o módulo e parsear a grade". Esta consulta exercita o caminho
/// inteiro — login, <c>AJAXREQUEST</c>, ViewState, busca, paginação e parser — em segundos e
/// <b>sem gravar uma linha</b> no nosso banco. É o ensaio que se faz antes de confiar numa
/// varredura de uma hora.</para>
///
/// <para><b>Cuidado operacional:</b> a sessão do SER é única por operador. Cada consulta aqui
/// derruba a sessão de quem estiver logado no SER com a credencial cadastrada.</para>
/// </summary>
public interface ISerConsultaDiretaService
{
    Task<SerConsultaDiretaDto> ConsultarAsync(
        SerConsultaDiretaRequest requisicao, CancellationToken cancellationToken);

    Task<SerHistoricoDiretoDto> HistoricoAsync(
        string idSer, SMSMais.Data.Entities.Ser.SituacaoSer situacao, CancellationToken cancellationToken);
}

public sealed class SerConsultaDiretaService(
    ISerLeitorService leitor,
    Integracoes.SerWeb.Varredura.Export.ISerExportLeitor exportLeitor) : ISerConsultaDiretaService
{
    /// <summary>5 páginas × 20 linhas: o teto da tela de Solicitação.</summary>
    private const int TetoPaginas = 5;

    /// <summary>A solicitação mais antiga vista em produção é de 2016.</summary>
    private static readonly DateOnly InicioPadrao = new(2015, 1, 1);

    public async Task<SerConsultaDiretaDto> ConsultarAsync(
        SerConsultaDiretaRequest r, CancellationToken cancellationToken)
    {
        if (r.PorExport) return await ConsultarPorExportAsync(r, cancellationToken);

        var relogio = Stopwatch.StartNew();

        await leitor.PrepararAsync(cancellationToken);

        var pagina = await leitor.PesquisarAsync(
            new SerFiltroPesquisa
            {
                Situacao = r.Situacao,
                Tipo = r.Tipo,
                DataSolicitacaoInicio = r.DataSolicitacaoInicio,
                DataSolicitacaoFim = r.DataSolicitacaoFim,
                Cpf = Limpar(r.Cpf),
                Nome = Limpar(r.Nome),
                Cns = Limpar(r.Cns),
                IdSolicitacao = Limpar(r.IdSolicitacao),
            },
            cancellationToken);

        var linhas = pagina.Linhas;

        // Paginação é exercício de teste também: é onde mora a resposta PARCIAL do datascroller,
        // que não traz <form> e quebrava a primeira versão do motor.
        if (r.Pagina > 1)
        {
            if (r.Pagina > pagina.Paginas)
            {
                throw new ValidacaoException(
                    "ser.pagina_inexistente",
                    $"A consulta devolveu {pagina.Paginas} página(s); a {r.Pagina}ª não existe.");
            }
            linhas = await leitor.IrParaPaginaAsync(r.Pagina, cancellationToken);
        }

        relogio.Stop();

        return new SerConsultaDiretaDto(
            linhas.Select(l => new SerLinhaDiretaDto(
                l.IdSer, l.Tipo, l.Recurso, l.DataSolicitacao, l.Paciente, l.Idade, l.Cpf,
                l.Cns, l.Cid, l.Solicitante, l.MunicipioSolicitante, l.AgendadoPara, l.Situacao)).ToList(),
            pagina.Paginas,
            pagina.Paginas >= TetoPaginas,
            (int)relogio.ElapsedMilliseconds,
            FonteConsultaSer.TelaSolicitacao,
            // A tela de Solicitação corta calada: não existe aviso nenhum para repassar.
            AvisoDoSer: null);
    }

    /// <summary>
    /// Consulta pela tela de Histórico (export de 500) — o caminho que a varredura usa de verdade.
    ///
    /// <para><b>É o único que permite contar.</b> A tela de Solicitação trava em 100 por
    /// construção, então "bateu no teto" ali não diz se existem 101 ou 8.000 registros. Aqui o
    /// número de linhas é real até 500, e o SER avisa por escrito quando cortou — é isso que
    /// transforma conferência de cobertura em algo verificável.</para>
    ///
    /// <para>Somente leitura, como o resto desta bancada: baixa a planilha, parseia em memória e
    /// <b>não grava nada</b> no nosso banco.</para>
    /// </summary>
    private async Task<SerConsultaDiretaDto> ConsultarPorExportAsync(
        SerConsultaDiretaRequest r, CancellationToken cancellationToken)
    {
        if (r.Situacao == SMSMais.Data.Entities.Ser.SituacaoSer.Alta)
        {
            throw new ValidacaoException(
                "ser.export_sem_alta",
                "A tela de Histórico do SER não oferece a situação ALTA no combo. Para conferir "
                + "ALTA, use a consulta pela tela de Solicitação (que trava em 100).");
        }

        var relogio = Stopwatch.StartNew();

        await exportLeitor.PrepararAsync(cancellationToken);

        var lote = await exportLeitor.ExportarAsync(
            new SerFiltroExport
            {
                Situacao = r.Situacao,
                Tipo = r.Tipo,
                DataSolicitacaoInicio = r.DataSolicitacaoInicio ?? InicioPadrao,
                DataSolicitacaoFim = r.DataSolicitacaoFim
                                     ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                UnidadeSolicitante = r.FiltrarPorSolicitante ? "GESTOR SMS MARICA" : null,
            },
            cancellationToken);

        relogio.Stop();

        return new SerConsultaDiretaDto(
            lote.Linhas.Select(l => new SerLinhaDiretaDto(
                l.IdSer, l.Tipo, l.Recurso, l.DataSolicitacao, l.Paciente, l.Idade, l.Cpf,
                l.Cns, l.Cid, l.Solicitante, l.MunicipioSolicitante, l.AgendadoPara, l.Situacao)).ToList(),
            Paginas: 0,
            BateuNoTeto: lote.Truncado,
            (int)relogio.ElapsedMilliseconds,
            FonteConsultaSer.ExportHistorico,
            lote.Aviso);
    }

    public async Task<SerHistoricoDiretoDto> HistoricoAsync(
        string idSer, SMSMais.Data.Entities.Ser.SituacaoSer situacao, CancellationToken cancellationToken)
    {
        var relogio = Stopwatch.StartNew();
        await leitor.PrepararAsync(cancellationToken);

        try
        {
            var historico = await leitor.LerHistoricoPorIdAsync(idSer, situacao, cancellationToken);
            relogio.Stop();

            return new SerHistoricoDiretoDto(
                idSer,
                historico.Paciente,
                historico.Eventos.Select(e => new SerEventoDiretoDto(
                    e.Data, e.Evento, e.EstadoAnterior, e.EstadoAtual, e.CentralRegulacao,
                    e.UnidadeExecutora, e.Usuario, e.LotacaoEvento, e.Ip, e.Observacao)).ToList(),
                (int)relogio.ElapsedMilliseconds);
        }
        catch (HistoricoSerIndisponivelException ex)
        {
            // Vira 400 com mensagem explicando, em vez de 500: não é erro do sistema, é o SER
            // não oferecendo o item (acontece com situação Alta).
            throw new ValidacaoException("ser.historico_indisponivel", ex.Message);
        }
    }

    private static string? Limpar(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
