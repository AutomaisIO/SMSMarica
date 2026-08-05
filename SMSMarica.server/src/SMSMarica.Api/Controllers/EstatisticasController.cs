using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Estatisticas;
using SMSMarica.Core.Estatisticas.Dtos;
using SMSMarica.Data.Entities.Enums;

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
        CancellationToken ct = default)
    {
        var fim = ate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var inicio = de ?? fim.AddDays(-29);
        return await service.ObterExamesImagemAsync(inicio, fim, unidadeId, ct);
    }

    /// <summary>
    /// Exporta a lista ANALÍTICA (CSV) que sustenta os agregados de imagem — uma linha por exame,
    /// com PII de paciente. Gate próprio (<see cref="ModuloPermissao.SolicitacoesExame"/>): quem
    /// exporta identificação de paciente é quem já pode ver a listagem de exames. A leitura é
    /// auditada no serviço (quem/quando/recorte/quantidade).
    /// </summary>
    [HttpGet("exames-imagem/exportar")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportarExamesImagem(
        [FromQuery] DateOnly? de = null,
        [FromQuery] DateOnly? ate = null,
        [FromQuery] Guid? unidadeId = null,
        CancellationToken ct = default)
    {
        var fim = ate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var inicio = de ?? fim.AddDays(-29);
        var linhas = await service.ListarAnaliticoExamesImagemAsync(inicio, fim, unidadeId, ct);

        var csv = MontarCsv(linhas);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        var nome = $"exames-imagem_{inicio:yyyy-MM-dd}_a_{fim:yyyy-MM-dd}.csv";
        return File(bytes, "text/csv; charset=utf-8", nome);
    }

    // ---- CSV (separador ';' + BOM UTF-8: abre direto no Excel pt-BR) ----

    private static readonly string[] Cabecalho =
    [
        "Nº solicitação", "Accession", "Paciente", "CPF", "CNS", "Nascimento",
        "Modalidade", "Tipo de exame", "Unidade executante", "Unidade solicitante", "Status",
        "Data solicitação", "Autorizado em", "Data do estudo", "Realizado em",
        "Laudo finalizado em", "Médico do laudo", "CRM",
        "Chegada→execução (h)", "Execução→laudo (h)", "Total (h)",
    ];

    private static string MontarCsv(IReadOnlyList<ExameImagemAnaliticoDto> linhas)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', Cabecalho.Select(Escapar)));
        foreach (var l in linhas)
        {
            string[] campos =
            [
                l.NumeroSolicitacao ?? "", l.AccessionNumber, l.PacienteNome ?? "",
                l.PacienteCpf ?? "", l.PacienteCns ?? "", Data(l.PacienteNascimento),
                l.Modalidade, l.TipoExame ?? "", l.UnidadeExecutante ?? "", l.UnidadeSolicitante ?? "",
                l.Status, Data(l.DataSolicitacao), DataHora(l.AutorizadoEm), DataHora(l.DataEstudo),
                DataHora(l.RealizadoEm), DataHora(l.LaudoFinalizadoEm), l.MedicoLaudo ?? "", l.MedicoCrm ?? "",
                Num(l.TempoChegadaExecucaoHoras), Num(l.TempoExecucaoLaudoHoras), Num(l.TempoTotalHoras),
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
