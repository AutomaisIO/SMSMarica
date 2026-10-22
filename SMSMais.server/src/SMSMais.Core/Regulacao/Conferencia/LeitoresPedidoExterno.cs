using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SMSMais.Core.Integracoes.EsusSgWeb;
using SMSMais.Core.Integracoes.SernitWeb;
using SMSMais.Core.Integracoes.SernitWeb.Varredura;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Integracoes.SerWeb.Varredura;
using SMSMais.Core.Integracoes.SisregWeb;
using SMSMais.Core.Integracoes.SisregWeb.Chave;
using SMSMais.Data.Entities.EsusSg;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sernit;
using SMSMais.Data.Entities.Ser;

namespace SMSMais.Core.Regulacao.Conferencia;

/// <summary>
/// SISREG: a FICHA da solicitação (<c>cons_marcados_reg</c>, e o <c>gerenciador_solicitacao</c> se a
/// primeira não a trouxer), com o operador da integração. Custa 1–2 requisições do orçamento
/// anti-robô, da reserva dos cliques humanos — é uma pessoa que pediu.
///
/// <para>A ficha é a fonte mais rica dos quatro sistemas: traz a unidade solicitante com CNES — o
/// que mais importa no NAR —, médico, CID, risco e os procedimentos com código interno.</para>
/// </summary>
public sealed class LeitorPedidoSisreg(
    IChaveConfirmacaoSisregService fichas,
    IOptions<SisregOrcamentoOpcoes> orcamento) : ILeitorPedidoExterno
{
    public SistemaRegulacao Sistema => SistemaRegulacao.Sisreg;

    public async Task<PedidoNoSistema?> LerAsync(string numero, CancellationToken ct)
    {
        var lida = await fichas.LerFichaPorCodigoAsync(numero, orcamento.Value.TetoPorHora, ct);
        var ficha = FichaSisregHtmlParser.Ler(lida.Html);

        // Página sem ficha, ou ficha de outro número: para a conferência, o pedido não foi achado.
        if (ficha is null || (ficha.CodigoSolicitacao is { } co && co != numero)) return null;

        return DaFicha(ficha);
    }

    /// <summary>Puro — testável com o HTML capturado. A chave de confirmação fica de fora de propósito.</summary>
    public static PedidoNoSistema DaFicha(FichaSisreg ficha)
    {
        var solicitacao = Secao(ficha, "dados da solicitacao");
        var nomes = solicitacao?.Campos.Where(c => Igual(c.Rotulo, "Procedimentos Solicitados")).Select(c => c.Valor).ToList() ?? [];
        var codigos = solicitacao?.Campos.Where(c => Igual(c.Rotulo, "Cód. Interno")).Select(c => c.Valor).ToList() ?? [];
        var procedimentos = nomes
            .Select((nome, i) => new ProcedimentoNoSistema(nome, i < codigos.Count ? codigos[i] : null))
            .ToList();

        // CNES aparece nas duas unidades (solicitante e executante): tem de ser o da seção certa.
        var unidade = Secao(ficha, "unidade solicitante");

        return new PedidoNoSistema(
            Situacao: ficha.Valor("Situação Atual"),
            PacienteNome: ficha.Valor("Nome do Paciente"),
            PacienteCpf: null,
            PacienteCns: Secao(ficha, "dados do paciente")?.Campos.FirstOrDefault(c => Igual(c.Rotulo, "CNS"))?.Valor,
            Procedimentos: procedimentos,
            Cid: ficha.Valor("CID"),
            UnidadeSolicitante: unidade?.Campos.FirstOrDefault(c => Igual(c.Rotulo, "Unidade Solicitante"))?.Valor,
            UnidadeSolicitanteCnes: unidade?.Campos.FirstOrDefault(c => Igual(c.Rotulo, "Cód. CNES"))?.Valor,
            MedicoNome: ficha.Valor("Nome Médico Solicitante"),
            MedicoCpf: ficha.Valor("CPF do Médico Solicitante"),
            Risco: ficha.Valor("Risco"),
            DataSolicitacao: ficha.Valor("Data Solicitação"));
    }

    private static SecaoFichaSisreg? Secao(FichaSisreg ficha, string titulo) =>
        ficha.Secoes.FirstOrDefault(s => FichaSisregHtmlParser.Normalizar(s.Titulo) == titulo);

    private static bool Igual(string rotulo, string procurado) =>
        FichaSisregHtmlParser.Normalizar(rotulo) == FichaSisregHtmlParser.Normalizar(procurado);
}

/// <summary>
/// SER: a grade da tela de Solicitação filtrada pelo ID, com a credencial de sincronismo. A tela
/// exige a situação, então tenta as três em que um pedido recém-lançado pode estar e para na
/// primeira que o acha. Cada tentativa custa um GET e um POST; não há orçamento anti-robô no SER,
/// mas a sessão é única por login — a conferência disputa a credencial com a varredura.
/// </summary>
public sealed class LeitorPedidoSer(ISerLeitorService leitor, ILogger<LeitorPedidoSer> logger) : ILeitorPedidoExterno
{
    private static readonly SituacaoSer[] Situacoes = [SituacaoSer.EmFila, SituacaoSer.Pendente, SituacaoSer.Agendada];

    public SistemaRegulacao Sistema => SistemaRegulacao.Ser;

    public async Task<PedidoNoSistema?> LerAsync(string numero, CancellationToken ct)
    {
        await leitor.PrepararAsync(ct);
        foreach (var situacao in Situacoes)
        {
            SerPaginaGrade pagina;
            try
            {
                pagina = await leitor.PesquisarAsync(new SerFiltroPesquisa { Situacao = situacao, IdSolicitacao = numero }, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "SER: pesquisa do pedido em {Situacao} falhou (conferência).", situacao);
                continue;
            }

            if (pagina.Linhas.FirstOrDefault(l => l.IdSer == numero) is { } l)
            {
                return new PedidoNoSistema(
                    l.Situacao ?? situacao.ToString(), l.Paciente, l.Cpf, l.Cns,
                    string.IsNullOrWhiteSpace(l.Recurso) ? [] : [new ProcedimentoNoSistema(l.Recurso, null)],
                    l.Cid,
                    // A grade do SER mostra sempre "GESTOR SMS MARICA" como solicitante: não é a unidade.
                    UnidadeSolicitante: null, UnidadeSolicitanteCnes: null,
                    MedicoNome: null, MedicoCpf: null, Risco: null,
                    l.DataSolicitacao);
            }
        }
        return null;
    }
}

/// <summary>SERNIT: o irmão do SER, com o mesmo jeito de pesquisar e o mesmo cuidado com a sessão.</summary>
public sealed class LeitorPedidoSernit(ISernitLeitorService leitor, ILogger<LeitorPedidoSernit> logger) : ILeitorPedidoExterno
{
    private static readonly SituacaoSernit[] Situacoes = [SituacaoSernit.EmFila, SituacaoSernit.Pendente, SituacaoSernit.Agendada];

    public SistemaRegulacao Sistema => SistemaRegulacao.Sernit;

    public async Task<PedidoNoSistema?> LerAsync(string numero, CancellationToken ct)
    {
        await leitor.PrepararAsync(ct);
        foreach (var situacao in Situacoes)
        {
            SernitPaginaGrade pagina;
            try
            {
                pagina = await leitor.PesquisarAsync(new SernitFiltroPesquisa { Situacao = situacao, IdSolicitacao = numero }, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "SERNIT: pesquisa do pedido em {Situacao} falhou (conferência).", situacao);
                continue;
            }

            if (pagina.Linhas.FirstOrDefault(l => l.IdSernit == numero) is { } l)
            {
                return new PedidoNoSistema(
                    l.Situacao ?? situacao.ToString(), l.Paciente, l.Cpf, l.Cns,
                    string.IsNullOrWhiteSpace(l.Recurso) ? [] : [new ProcedimentoNoSistema(l.Recurso, null)],
                    l.Cid,
                    UnidadeSolicitante: null, UnidadeSolicitanteCnes: null,
                    MedicoNome: null, MedicoCpf: null, Risco: null,
                    l.DataSolicitacao);
            }
        }
        return null;
    }
}

/// <summary>
/// ESUS de São Gonçalo (ADR-0063): não há leitura por número; a fila inteira vem numa requisição
/// (~650 linhas, ~1,4 s) e o pedido é achado pelo <c>fil_id</c>. Exame primeiro — é o que Maricá
/// usa —, consulta depois. Pedido que já saiu da fila (agendado) não é achado aqui.
/// </summary>
public sealed class LeitorPedidoEsusSg(IEsusSgLeitorService leitor) : ILeitorPedidoExterno
{
    public SistemaRegulacao Sistema => SistemaRegulacao.EsusSg;

    public async Task<PedidoNoSistema?> LerAsync(string numero, CancellationToken ct)
    {
        foreach (var tipo in new[] { TipoRecursoEsusSg.Exame, TipoRecursoEsusSg.Consulta })
        {
            var fila = await leitor.LerFilaAsync(tipo, ct);
            if (fila.Linhas.FirstOrDefault(l => l.IdEsusSg == numero) is { } l)
            {
                return new PedidoNoSistema(
                    Situacao: string.IsNullOrWhiteSpace(l.Pendencia) ? "Na fila" : $"Na fila — pendência: {l.Pendencia}",
                    l.PacienteNome, l.Cpf, l.Cns,
                    [new ProcedimentoNoSistema(l.Recurso, l.CodigoInterno)],
                    Cid: null,
                    l.UnidadeSolicitante, UnidadeSolicitanteCnes: null,
                    l.ProfissionalSolicitante, MedicoCpf: null,
                    l.Prioridade,
                    l.DataSolicitacao?.ToString("dd/MM/yyyy"));
            }
        }
        return null;
    }
}
