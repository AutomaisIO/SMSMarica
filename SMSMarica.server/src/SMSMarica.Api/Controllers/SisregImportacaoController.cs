using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.SisregWeb.Importacao;
using SMSMarica.Core.Integracoes.SisregWeb.Importacao.Background;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Importação de agendamentos do SISREG (export de agendamentos — expo_solicitacoes) para
/// SolicitacaoExame. Aceita TXT (cabeçalho de unidade) ou CSV (cabeçalho de colunas; a unidade
/// executante vem do nome do arquivo). PREVIEW (só leitura) mostra o "diff" (novo vs já existe);
/// EXECUTAR importa uma marcação por vez.
/// </summary>
[ApiController]
[Route("sisreg/importacao")]
public sealed class SisregImportacaoController(
    IImportacaoSisregService importacao,
    IImportacaoLoteService lote) : ControllerBase
{
    /// <summary>Extensões que o sistema sequer abre. Fora disto, o arquivo é ignorado antes de
    /// qualquer leitura — não vira erro nem linha de rastreio, só é sinalizado na resposta.</summary>
    private static readonly string[] ExtensoesAceitas = [".txt", ".csv"];

    /// <summary>UTF-8 que LANÇA em bytes inválidos — é o que permite detectar "não é UTF-8".</summary>
    private static readonly UTF8Encoding Utf8Estrito = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Lê o texto do SISREG detectando o encoding. O export vem ora em UTF-8, ora em ISO-8859-1
    /// (latin-1) — ler tudo como latin-1 corrompia os acentos dos arquivos UTF-8 (ex.: "AVALIAÇÃO"
    /// virava "AVALIAÃÃO", porque cada byte do "Ç" UTF-8 vira um caractere latin-1). Estratégia:
    /// tenta UTF-8 ESTRITO; como quase todo texto latin-1 acentuado NÃO é UTF-8 válido, a exceção
    /// separa os dois com segurança. Latin-1 aceita qualquer byte, então é o fallback natural.
    /// </summary>
    private static async Task<string> LerTextoSisregAsync(Stream stream, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();

        // BOM UTF-8 no início: é UTF-8 sem ambiguidade; pula os 3 bytes para não deixar U+FEFF.
        var inicio = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        try
        {
            return Utf8Estrito.GetString(bytes, inicio, bytes.Length - inicio);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    /// <summary>Preview a partir do upload do arquivo (TXT ou CSV). Não escreve nada.</summary>
    [HttpPost("preview")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<ImportacaoPreviewResultado>(StatusCodes.Status200OK)]
    [RequestSizeLimit(20_000_000)]
    public async Task<ImportacaoPreviewResultado> Preview(
        IFormFile arquivo,
        CancellationToken cancellationToken)
    {
        if (arquivo is null || arquivo.Length == 0)
            throw new ValidacaoException("importacao.arquivo_ausente", "Envie o arquivo (TXT ou CSV) do SISREG.");

        var conteudo = await LerTextoSisregAsync(arquivo.OpenReadStream(), cancellationToken);
        return await importacao.PreviewDeTextoAsync(conteudo, arquivo.FileName, cancellationToken);
    }

    /// <summary>Importa UMA marcação (por código) do arquivo enviado — roda o fluxo inteiro. ESCRITA.</summary>
    [HttpPost("executar")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Inclusao)]
    [ProducesResponseType<ImportacaoExecucaoResultado>(StatusCodes.Status200OK)]
    [RequestSizeLimit(20_000_000)]
    public async Task<ImportacaoExecucaoResultado> Executar(
        IFormFile arquivo,
        [FromForm] string codigo,
        CancellationToken cancellationToken)
    {
        if (arquivo is null || arquivo.Length == 0)
            throw new ValidacaoException("importacao.arquivo_ausente", "Envie o arquivo (TXT ou CSV) do SISREG.");
        if (string.IsNullOrWhiteSpace(codigo))
            throw new ValidacaoException("importacao.codigo_ausente", "Informe o código da marcação a importar.");

        var conteudo = await LerTextoSisregAsync(arquivo.OpenReadStream(), cancellationToken);
        return await importacao.ExecutarUmAsync(conteudo, codigo, arquivo.FileName, cancellationToken);
    }

    /// <summary>Linhas do SISREG que não viraram solicitação (com o RAW). Pendentes por padrão.</summary>
    [HttpGet("falhas")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ImportacaoFalhaDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ImportacaoFalhaDto>> ListarFalhas(
        [FromQuery] bool somentePendentes = true,
        [FromQuery] string? busca = null,
        CancellationToken cancellationToken = default)
        => await importacao.ListarFalhasAsync(somentePendentes, busca, cancellationToken);

    /// <summary>"Validar": reimporta a linha a partir do RAW guardado — não precisa do arquivo de
    /// novo. Se a solicitação já existir, resolve a falha em vez de duplicar. ESCRITA.</summary>
    [HttpPost("falhas/{id:guid}/reprocessar")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Inclusao)]
    [ProducesResponseType<ImportacaoFalhaReprocessoResultado>(StatusCodes.Status200OK)]
    public async Task<ImportacaoFalhaReprocessoResultado> ReprocessarFalha(
        Guid id,
        CancellationToken cancellationToken)
        => await importacao.ReprocessarFalhaAsync(id, cancellationToken);

    /// <summary>
    /// Pendências de SIGTAP agrupadas por procedimento. Uma varredura sem mapeamento gera uma
    /// pendência por solicitação — todas com a mesma causa e a mesma correção. Agrupadas, viram a
    /// fila de trabalho de quem vai mapear, em vez de ruído que esconde as pendências individuais.
    /// </summary>
    [HttpGet("falhas/sigtap")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<PendenciaSigtapAgrupadaDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<PendenciaSigtapAgrupadaDto>> PendenciasSigtap(
        CancellationToken cancellationToken)
        => await importacao.ListarPendenciasSigtapAsync(cancellationToken);

    /// <summary>
    /// Revalida TODAS as pendências de SIGTAP de um procedimento — o par do mapeamento: mapeia-se
    /// uma vez, as solicitações entram de uma vez. ESCRITA.
    /// </summary>
    [HttpPost("falhas/sigtap/reprocessar")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Inclusao)]
    [ProducesResponseType<ReprocessoLoteResultado>(StatusCodes.Status200OK)]
    public async Task<ReprocessoLoteResultado> ReprocessarPendenciasSigtap(
        [FromBody] ReprocessarSigtapRequest request,
        CancellationToken cancellationToken)
        => await importacao.ReprocessarPendenciasSigtapAsync(request.ProcedimentoTexto, cancellationToken);

    /// <summary>
    /// "Informar CPF e importar" (ADR-0035): resolve o paciente e replica a linha com ele fixado.
    /// É a ação da pendência cuja causa é <c>CpfNaoResolvido</c> — o CADSUS não devolveu o CPF e o
    /// paciente não existia. Só vincula paciente JÁ cadastrado: o SISREG não informa data de
    /// nascimento, então cadastrar por aqui gravaria data default no hub.
    /// </summary>
    [HttpPost("falhas/{id:guid}/resolver-com-paciente")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Inclusao)]
    [ProducesResponseType<ImportacaoFalhaReprocessoResultado>(StatusCodes.Status200OK)]
    public async Task<ImportacaoFalhaReprocessoResultado> ResolverFalhaComPaciente(
        Guid id,
        [FromBody] ResolverFalhaComPacienteRequest corpo,
        CancellationToken cancellationToken)
    {
        if (corpo is null || (string.IsNullOrWhiteSpace(corpo.Cpf) && corpo.PacienteId is null))
            throw new ValidacaoException("falha.paciente_ausente", "Informe o CPF ou selecione o paciente.");

        return await importacao.ResolverComPacienteAsync(id, corpo.Cpf, corpo.PacienteId, cancellationToken);
    }

    /// <summary>Tira a linha da lista sem importar (inválida na origem, registro cancelado…).
    /// Não apaga nada: só marca a falha como resolvida. Mesma permissão do importar — quem toca
    /// a fila de importação é quem tria os erros dela.</summary>
    [HttpPost("falhas/{id:guid}/descartar")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Inclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DescartarFalha(
        Guid id,
        [FromBody] DescartarFalhaRequest? corpo,
        CancellationToken cancellationToken)
    {
        await importacao.DescartarFalhaAsync(id, corpo?.Nota, cancellationToken);
        return NoContent();
    }

    /// <summary>Detalhe da falha para o modal: o RAW + o parse dele (campos do SISREG nomeados).</summary>
    [HttpGet("falhas/{id:guid}/detalhe")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<ImportacaoFalhaDetalheDto>(StatusCodes.Status200OK)]
    public async Task<ImportacaoFalhaDetalheDto> DetalheFalha(Guid id, CancellationToken cancellationToken)
        => await importacao.ObterFalhaDetalheAsync(id, cancellationToken);

    // ===================== LOTE (vários arquivos / zip / diretório) =====================

    /// <summary>
    /// Envia vários arquivos de uma vez (seleção múltipla, diretório ou .zip) e importa TUDO no
    /// servidor, em background. Responde 202 na hora — o front acompanha por <c>GET lote/status</c>.
    /// <para>
    /// Arquivos com extensão fora de .txt/.csv são IGNORADOS sem serem abertos (nem viram erro,
    /// só são listados na resposta). O que é .txt/.csv mas não é do SISREG vira "Arquivo
    /// incompatível" na aba Erros — aí é sinal de que alguém mandou o arquivo errado.
    /// </para>
    /// </summary>
    [HttpPost("lote")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Inclusao)]
    [ProducesResponseType<ImportacaoLoteAceitoDto>(StatusCodes.Status202Accepted)]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> ImportarLote(
        [FromForm] IFormFileCollection arquivos,
        CancellationToken cancellationToken)
    {
        if (arquivos is null || arquivos.Count == 0)
            throw new ValidacaoException("importacao.arquivo_ausente", "Envie ao menos um arquivo.");

        var aceitos = new List<ArquivoRecebido>();
        var ignorados = new List<string>();

        foreach (var f in arquivos)
        {
            if (EhZip(f.FileName))
            {
                await ExtrairZipAsync(f, aceitos, ignorados, cancellationToken);
                continue;
            }
            if (!ExtensaoAceita(f.FileName)) { ignorados.Add(f.FileName); continue; }

            var conteudo = await LerTextoSisregAsync(f.OpenReadStream(), cancellationToken);
            aceitos.Add(new ArquivoRecebido(f.FileName, conteudo, null));
        }

        var loteId = await lote.IniciarAsync(aceitos, cancellationToken);
        return Accepted(new ImportacaoLoteAceitoDto(loteId, aceitos.Count, ignorados));
    }

    /// <summary>Progresso do lote em andamento (ou o resumo do último). Polling.</summary>
    [HttpGet("lote/status")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<StatusLote>(StatusCodes.Status200OK)]
    public async Task<StatusLote?> StatusLote(CancellationToken cancellationToken)
        => await lote.ObterStatusAsync(cancellationToken);

    /// <summary>Para a importação em andamento. O que já entrou permanece (não faz rollback).</summary>
    [HttpPost("lote/cancelar")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Inclusao)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult CancelarLote() => Accepted(new { cancelado = lote.Cancelar() });

    /// <summary>Aba de rastreio: uma linha por arquivo importado (quando, quem, válidos, inválidos).</summary>
    [HttpGet("execucoes")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ImportacaoExecucaoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ImportacaoExecucaoDto>> ListarExecucoes(
        [FromQuery] int limite = 100,
        CancellationToken cancellationToken = default)
        => await lote.ListarExecucoesAsync(limite, cancellationToken);

    private static bool ExtensaoAceita(string nome) =>
        ExtensoesAceitas.Contains(Path.GetExtension(nome), StringComparer.OrdinalIgnoreCase);

    private static bool EhZip(string nome) =>
        string.Equals(Path.GetExtension(nome), ".zip", StringComparison.OrdinalIgnoreCase);

    /// <summary>Abre o zip e pega só os .txt/.csv de dentro (em qualquer subpasta).</summary>
    private static async Task ExtrairZipAsync(
        IFormFile zip, List<ArquivoRecebido> aceitos, List<string> ignorados, CancellationToken ct)
    {
        // Copia pra memória: ZipArchive precisa de stream seekable.
        using var buffer = new MemoryStream();
        await zip.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);
        foreach (var entrada in archive.Entries)
        {
            // Entrada de diretório (nome vazio) — não é arquivo.
            if (string.IsNullOrEmpty(entrada.Name)) continue;

            if (!ExtensaoAceita(entrada.Name))
            {
                ignorados.Add($"{zip.FileName} → {entrada.FullName}");
                continue;
            }

            using var s = entrada.Open();
            var conteudo = await LerTextoSisregAsync(s, ct);
            aceitos.Add(new ArquivoRecebido(entrada.Name, conteudo, entrada.FullName));
        }
    }
}

/// <summary>Motivo do descarte (opcional) — fica na trilha da falha.</summary>
public sealed record DescartarFalhaRequest(string? Nota);

/// <summary>
/// Quem é o paciente desta pendência. <c>PacienteId</c> tem precedência (o operador escolheu na
/// busca, olhando nome e nascimento); <c>Cpf</c> é o atalho de digitação. Um dos dois é obrigatório.
/// </summary>
public sealed record ResolverFalhaComPacienteRequest(string? Cpf, Guid? PacienteId);
