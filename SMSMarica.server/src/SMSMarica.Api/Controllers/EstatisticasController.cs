using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Estatisticas;
using SMSMarica.Core.Estatisticas.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Estatísticas gerenciais de atendimento (retrato das comunicações WhatsApp). Visão global,
/// só leitura — protegida por <see cref="ModuloPermissao.Estatistica"/>.
/// </summary>
[ApiController]
[Route("estatisticas")]
public sealed class EstatisticasController(IEstatisticasService service) : ControllerBase
{
    /// <summary>
    /// Retrato do WhatsApp no período. Sem <c>de</c>/<c>ate</c>, usa os últimos 30 dias.
    /// </summary>
    [HttpGet("whatsapp")]
    [RequerPermissao(ModuloPermissao.Estatistica, AcoesPermissao.Consulta)]
    [ProducesResponseType<EstatisticasWhatsAppDto>(StatusCodes.Status200OK)]
    public async Task<EstatisticasWhatsAppDto> WhatsApp(
        [FromQuery] DateOnly? de = null,
        [FromQuery] DateOnly? ate = null,
        CancellationToken ct = default)
    {
        var fim = ate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var inicio = de ?? fim.AddDays(-29);
        return await service.ObterWhatsAppAsync(inicio, fim, ct);
    }

    /// <summary>
    /// Agregados dos exames de imagem no período (últimos 30 dias por padrão), no escopo de unidade
    /// do usuário e, opcionalmente, restrito a uma unidade executante. Só contagens/médias — sem PII.
    /// </summary>
    [HttpGet("exames-imagem")]
    [RequerPermissao(ModuloPermissao.Estatistica, AcoesPermissao.Consulta)]
    [ProducesResponseType<EstatisticasExamesImagemDto>(StatusCodes.Status200OK)]
    public async Task<EstatisticasExamesImagemDto> ExamesImagem(
        [FromQuery] DateOnly? de = null,
        [FromQuery] DateOnly? ate = null,
        [FromQuery] Guid? unidadeId = null,
        [FromQuery] ModalidadeDicom? modalidade = null,
        [FromQuery] Guid? tipoExameId = null,
        CancellationToken ct = default)
    {
        var fim = ate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var inicio = de ?? fim.AddDays(-29);
        return await service.ObterExamesImagemAsync(inicio, fim, unidadeId, modalidade, tipoExameId, ct);
    }

    /// <summary>
    /// Exporta a lista ANALÍTICA (CSV) que sustenta os agregados de imagem, no conteúdo escolhido
    /// (<c>Exames</c>, <c>Laudos</c> ou <c>ExamesLaudos</c>). SEM PII de paciente — só números do
    /// exame/solicitação/laudo (decisão do ticket #94). Gate <see cref="ModuloPermissao.SolicitacoesExame"/>
    /// (nível da listagem de exames); a leitura é auditada no serviço.
    /// </summary>
    [HttpGet("exames-imagem/exportar")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportarExamesImagem(
        [FromQuery] DateOnly? de = null,
        [FromQuery] DateOnly? ate = null,
        [FromQuery] Guid? unidadeId = null,
        [FromQuery] ModalidadeDicom? modalidade = null,
        [FromQuery] Guid? tipoExameId = null,
        [FromQuery] ConteudoExportacaoImagem conteudo = ConteudoExportacaoImagem.ExamesLaudos,
        CancellationToken ct = default)
    {
        var fim = ate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var inicio = de ?? fim.AddDays(-29);
        var dados = await service.ObterExportacaoImagemAsync(
            inicio, fim, unidadeId, modalidade, tipoExameId, conteudo, ct);

        var (csv, sufixo) = conteudo switch
        {
            ConteudoExportacaoImagem.Laudos => (MontarCsvLaudos(dados.Laudos), "laudos"),
            ConteudoExportacaoImagem.Exames => (MontarCsvExames(dados.Exames, incluirLaudo: false), "exames"),
            _ => (MontarCsvExames(dados.Exames, incluirLaudo: true), "exames-e-laudos"),
        };

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        var nome = $"imagem-{sufixo}_{inicio:yyyy-MM-dd}_a_{fim:yyyy-MM-dd}.csv";
        return File(bytes, "text/csv; charset=utf-8", nome);
    }

    /// <summary>
    /// Lista de FATURAMENTO dos exames de imagem realizados no período (JSON). Uma linha por exame, da
    /// menor para a maior data de realização, COM PII do paciente (nome/CPF/CNS/nascimento/CEP/celular)
    /// necessária ao faturamento — ticket #74. Sem accession nem números internos. O painel monta o
    /// .xlsx formatado a partir desta lista. Mesmo gate/escopo da exportação; a leitura é auditada.
    /// </summary>
    [HttpGet("exames-imagem/faturamento")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ExameFaturamentoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ExameFaturamentoDto>> FaturamentoExamesImagem(
        [FromQuery] DateOnly? de = null,
        [FromQuery] DateOnly? ate = null,
        [FromQuery] Guid? unidadeId = null,
        [FromQuery] ModalidadeDicom? modalidade = null,
        [FromQuery] Guid? tipoExameId = null,
        CancellationToken ct = default)
    {
        var fim = ate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var inicio = de ?? fim.AddDays(-29);
        return await service.ObterFaturamentoImagemAsync(inicio, fim, unidadeId, modalidade, tipoExameId, ct);
    }

    // ---- CSV (separador ';' + BOM UTF-8: abre direto no Excel pt-BR) ----

    private static string MontarCsvExames(IReadOnlyList<ExameImagemAnaliticoDto> linhas, bool incluirLaudo)
    {
        string[] cab =
        [
            "Nº solicitação", "Accession", "Study UID", "Modalidade", "Tipo de exame",
            "Unidade executante", "Unidade solicitante", "Status",
            "Data solicitação", "Autorizado em", "Data do estudo", "Realizado em",
            .. incluirLaudo
                ? new[] { "Laudo finalizado em", "Médico do laudo", "CRM",
                          "Execução→laudo (h)", "Total (h)" }
                : Array.Empty<string>(),
            "Chegada→execução (h)",
        ];

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', cab.Select(Escapar)));
        foreach (var l in linhas)
        {
            string[] campos =
            [
                l.NumeroSolicitacao ?? "", l.AccessionNumber, l.StudyInstanceUID, l.Modalidade,
                l.TipoExame ?? "", l.UnidadeExecutante ?? "", l.UnidadeSolicitante ?? "", l.Status,
                Data(l.DataSolicitacao), DataHora(l.AutorizadoEm), DataHora(l.DataEstudo), DataHora(l.RealizadoEm),
                .. incluirLaudo
                    ? new[] { DataHora(l.LaudoFinalizadoEm), l.MedicoLaudo ?? "", l.MedicoCrm ?? "",
                              Num(l.TempoExecucaoLaudoHoras), Num(l.TempoTotalHoras) }
                    : Array.Empty<string>(),
                Num(l.TempoChegadaExecucaoHoras),
            ];
            sb.AppendLine(string.Join(';', campos.Select(Escapar)));
        }
        return sb.ToString();
    }

    private static string MontarCsvLaudos(IReadOnlyList<LaudoAnaliticoDto> linhas)
    {
        string[] cab =
        [
            "Nº solicitação", "Accession", "Study UID", "Versão", "Modalidade", "Tipo de exame",
            "Unidade executante", "Data do estudo", "Realizado em", "Laudo finalizado em",
            "Médico do laudo", "CRM", "Execução→laudo (h)",
            "Assinado em", "Laudo→assinatura (h)",
        ];

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', cab.Select(Escapar)));
        foreach (var l in linhas)
        {
            string[] campos =
            [
                l.NumeroSolicitacao ?? "", l.AccessionNumber, l.StudyInstanceUID,
                l.Versao.ToString(CultureInfo.InvariantCulture), l.Modalidade, l.TipoExame ?? "",
                l.UnidadeExecutante ?? "", DataHora(l.DataEstudo), DataHora(l.RealizadoEm),
                DataHora(l.LaudoFinalizadoEm), l.MedicoLaudo ?? "", l.MedicoCrm ?? "",
                Num(l.TempoExecucaoLaudoHoras),
                DataHora(l.AssinadoEm), Num(l.TempoLaudoAssinaturaHoras),
            ];
            sb.AppendLine(string.Join(';', campos.Select(Escapar)));
        }
        return sb.ToString();
    }

    private static string Data(DateOnly? d) => d?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "";
    private static string DataHora(DateTime? d) =>
        d?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) ?? "";
    private static string Num(double? v) =>
        v?.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') ?? "";

    private static string Escapar(string campo)
    {
        if (campo.IndexOfAny([';', '"', '\n', '\r']) < 0) return campo;
        return $"\"{campo.Replace("\"", "\"\"")}\"";
    }
}
