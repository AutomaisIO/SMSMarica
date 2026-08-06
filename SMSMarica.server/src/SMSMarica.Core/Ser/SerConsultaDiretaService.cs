using System.Diagnostics;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.SerWeb;
using SMSMarica.Core.Integracoes.SerWeb.Varredura;
using SMSMarica.Core.Ser.Dtos;

namespace SMSMarica.Core.Ser;

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
        string idSer, Data.Entities.Ser.SituacaoSer situacao, CancellationToken cancellationToken);
}

public sealed class SerConsultaDiretaService(ISerLeitorService leitor) : ISerConsultaDiretaService
{
    /// <summary>5 páginas × 20 linhas: o teto da tela do SER.</summary>
    private const int TetoPaginas = 5;

    public async Task<SerConsultaDiretaDto> ConsultarAsync(
        SerConsultaDiretaRequest r, CancellationToken cancellationToken)
    {
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
            (int)relogio.ElapsedMilliseconds);
    }

    public async Task<SerHistoricoDiretoDto> HistoricoAsync(
        string idSer, Data.Entities.Ser.SituacaoSer situacao, CancellationToken cancellationToken)
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
