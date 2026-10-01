using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.DocumentosPaciente;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Acervo do paciente — "Exames anexados" do cadastro: documentos do acervo, PDFs da anamnese,
/// laudos assinados e PDF das imagens dos exames, numa lista só.
/// </summary>
[ApiController]
[Route("pacientes/{pacienteId:guid}/documentos")]
public sealed class DocumentosPacienteController(IDocumentosPacienteService servico) : ControllerBase
{
    /// <summary>Acima do teto de 25 MB do arquivo: o erro amigável tem de vir do service.</summary>
    private const int LimiteCorpoBytes = 30 * 1024 * 1024;

    /// <summary>Tudo o que o paciente tem guardado, incluindo o que ainda espera conferência.</summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ItemAcervoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ItemAcervoDto>> Listar(Guid pacienteId, CancellationToken cancellationToken) =>
        servico.ListarAsync(pacienteId, incluirPendentes: true, cancellationToken);

    /// <summary>Conteúdo de um item (PDF ou imagem) para o visualizador.</summary>
    [HttpGet("{tipo}/{id:guid}/conteudo")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Consulta)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Conteudo(
        Guid pacienteId, TipoItemAcervo tipo, Guid id, CancellationToken cancellationToken)
    {
        var c = await servico.ObterConteudoAsync(pacienteId, tipo, id, permitirPendente: true, cancellationToken);
        return File(c.Conteudo, c.MimeType, c.NomeArquivo);
    }

    /// <summary>Envia um documento direto no cadastro (entra aceito — quem enviou é da equipe).</summary>
    [HttpPost]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Edicao)]
    [RequestSizeLimit(LimiteCorpoBytes)]
    [ProducesResponseType<ItemAcervoDto>(StatusCodes.Status200OK)]
    public async Task<ItemAcervoDto> Enviar(
        Guid pacienteId, IFormFile arquivo, [FromForm] string titulo, [FromForm] string? descricao,
        CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await arquivo.CopyToAsync(ms, cancellationToken);
        return await servico.AdicionarAsync(new NovoDocumentoPaciente(
            pacienteId, titulo, descricao, arquivo.FileName, arquivo.ContentType, ms.ToArray(),
            OrigemDocumentoPaciente.Painel, null, SituacaoDocumentoPaciente.Aceito), cancellationToken);
    }

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Edicao)]
    [ProducesResponseType<ItemAcervoDto>(StatusCodes.Status200OK)]
    public Task<ItemAcervoDto> Editar(
        Guid pacienteId, Guid id, [FromBody] EditarDocumentoPacienteRequest req, CancellationToken cancellationToken) =>
        servico.EditarAsync(pacienteId, id, req, cancellationToken);

    /// <summary>Aceita no cadastro o que o paciente enviou (pendente → aceito).</summary>
    [HttpPost("{id:guid}/aceitar")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Edicao)]
    [ProducesResponseType<ItemAcervoDto>(StatusCodes.Status200OK)]
    public Task<ItemAcervoDto> Aceitar(
        Guid pacienteId, Guid id, [FromBody] EditarDocumentoPacienteRequest? req, CancellationToken cancellationToken) =>
        servico.AceitarAsync(pacienteId, id, req, cancellationToken);

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Excluir(Guid pacienteId, Guid id, CancellationToken cancellationToken)
    {
        await servico.ExcluirAsync(pacienteId, id, cancellationToken);
        return NoContent();
    }
}
