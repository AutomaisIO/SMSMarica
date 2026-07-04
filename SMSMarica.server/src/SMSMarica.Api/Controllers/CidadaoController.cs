using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Atendimentos;
using SMSMarica.Core.Cidadao;
using SMSMarica.Core.Cidadao.Dtos;
using SMSMarica.Core.Pacientes;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Área autenticada do app do cidadão (token <c>tipo=cidadao</c>, single-device).
/// O paciente só enxerga/edita os próprios dados — o id vem do <c>sub</c> do token,
/// nunca do corpo/rota. Os endpoints clínicos hoje são stub (lista vazia); o
/// preenchimento virá das fontes numa próxima leva.
/// </summary>
[ApiController]
[Route("auth/paciente")]
[Authorize]
[ExigeConsentimento]
public sealed class CidadaoController(
    IPacientesService pacientes,
    IAtendimentosService atendimentos,
    ICidadaoSessaoService sessoes,
    IConsentimentoCidadaoService consentimentos,
    ICidadaoClinicoService clinico) : ControllerBase
{
    /// <summary>Status do consentimento LGPD + texto vigente do termo (acessível sem aceite).</summary>
    [HttpGet("consentimento")]
    [PermiteSemConsentimento]
    [ProducesResponseType<ConsentimentoStatusDto>(StatusCodes.Status200OK)]
    public Task<ConsentimentoStatusDto> ObterConsentimento(CancellationToken ct) =>
        consentimentos.ObterStatusAsync(PacienteId(), ct);

    /// <summary>Registra o aceite do termo vigente (acessível sem aceite — é o que cria o aceite).</summary>
    [HttpPost("consentimento")]
    [PermiteSemConsentimento]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AceitarConsentimento(CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var dispositivo = Request.Headers.UserAgent.ToString();
        await consentimentos.RegistrarAsync(PacienteId(), ip, dispositivo, ct);
        return NoContent();
    }

    [HttpGet("me")]
    [ProducesResponseType<PerfilCidadaoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PerfilCidadaoDto>> Me(CancellationToken ct)
    {
        var p = await pacientes.ObterPorIdAsync(PacienteId(), ct);
        return new PerfilCidadaoDto(
            p.Id, p.NomeCompleto, p.NomeSocial, p.Cpf, p.Cns, p.DataNascimento,
            p.Email, p.TelefonePrincipal, p.TelefoneCelular, p.TelefoneResidencial, p.FotoBase64);
    }

    [HttpPut("me/contato")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AtualizarContato(
        [FromBody] AtualizarContatoCidadaoRequest req, CancellationToken ct)
    {
        await pacientes.AtualizarContatoAsync(
            PacienteId(), req.Email, req.TelefonePrincipal, req.TelefoneCelular, req.TelefoneResidencial, ct);
        return NoContent();
    }

    [HttpPut("me/foto")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AtualizarFoto(
        [FromBody] AtualizarFotoCidadaoRequest req, CancellationToken ct)
    {
        await pacientes.AtualizarFotoAsync(PacienteId(), req.FotoBase64, ct);
        return NoContent();
    }

    [HttpPost("logout")]
    [PermiteSemConsentimento]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Jti() is Guid jti) await sessoes.RevogarAsync(jti, ct);
        return NoContent();
    }

    // --- Stubs clínicos: shape estável, lista vazia por enquanto. ---

    [HttpGet("meus-translados")]
    public ActionResult<IEnumerable<TransladoResumoDto>> MeusTranslados() =>
        Ok(Array.Empty<TransladoResumoDto>());

    /// <summary>
    /// Histórico de atendimentos do cidadão, lido do hub FHIR (Encounter + Condition).
    /// Projetado para o shape enxuto que a PWA consome.
    /// </summary>
    [HttpGet("atendimentos")]
    [ProducesResponseType<IEnumerable<AtendimentoResumoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AtendimentoResumoDto>>> Atendimentos(CancellationToken ct)
    {
        var lista = await atendimentos.ObterPorPacienteAsync(PacienteId(), ct);
        var resumos = lista.Select(a => new AtendimentoResumoDto(
            a.Id,
            (a.Inicio ?? a.Fim ?? default).DateTime,
            a.Tipo,
            a.MedicoNome ?? "Profissional não informado",
            DescricaoAtendimento(a),
            a.Documentos
                .Where(d => !string.IsNullOrWhiteSpace(d.ConteudoHtml))
                .Select(d => new DocumentoResumoDto(d.Id, d.Tipo, d.Data?.DateTime, d.ConteudoHtml))
                .ToList()));
        return Ok(resumos);
    }

    /// <summary>Resumo textual do atendimento a partir dos diagnósticos (CID-10).</summary>
    private static string DescricaoAtendimento(Core.Atendimentos.Dtos.AtendimentoDto a)
    {
        var diags = a.Diagnosticos
            .Select(d => string.IsNullOrWhiteSpace(d.Descricao) ? d.Codigo : d.Descricao)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        return diags.Count > 0 ? string.Join(" · ", diags) : string.Empty;
    }

    /// <summary>Exames realizados do paciente: documentos escaneados + imagens do PACS + laudo assinado (se houver).</summary>
    [HttpGet("exames")]
    [ProducesResponseType<IEnumerable<ExameResumoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ExameResumoDto>>> Exames(CancellationToken ct) =>
        Ok(await clinico.ListarExamesAsync(PacienteId(), ct));

    /// <summary>Baixa um documento escaneado (PDF) anexado a um exame do paciente.</summary>
    [HttpGet("anexos/{anexoId:guid}/conteudo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AnexoConteudo(Guid anexoId, CancellationToken ct)
    {
        var conteudo = await clinico.ObterAnexoAsync(PacienteId(), anexoId, ct);
        return conteudo is null
            ? NotFound()
            : File(conteudo.Conteudo, conteudo.MimeType, conteudo.NomeArquivo);
    }

    /// <summary>
    /// PDF consolidado das imagens do exame: gera sob demanda (busca as imagens no PACS, monta o
    /// documento com capa e armazena no S3) e reaproveita o cache nas próximas vezes.
    /// </summary>
    [HttpGet("exames/{solicitacaoExameId:guid}/imagens-pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ImagensPdf(Guid solicitacaoExameId, CancellationToken ct)
    {
        var pdf = await clinico.ObterImagensPdfAsync(PacienteId(), solicitacaoExameId, ct);
        return pdf is null
            ? NotFound()
            : File(pdf, "application/pdf", $"exame-imagens-{solicitacaoExameId}.pdf");
    }

    /// <summary>Laudos assinados (PAdES) do paciente.</summary>
    [HttpGet("laudos")]
    [ProducesResponseType<IEnumerable<LaudoResumoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LaudoResumoDto>>> Laudos(CancellationToken ct) =>
        Ok(await clinico.ListarLaudosAsync(PacienteId(), ct));

    /// <summary>Baixa o PDF assinado de um laudo do paciente.</summary>
    [HttpGet("laudos/{laudoId:guid}/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LaudoPdf(Guid laudoId, CancellationToken ct)
    {
        var pdf = await clinico.ObterLaudoPdfAsync(PacienteId(), laudoId, ct);
        return pdf is null
            ? NotFound()
            : File(pdf.Conteudo, "application/pdf", $"laudo-{laudoId}.pdf");
    }

    /// <summary>Consultas e exames agendados (futuros) do paciente. <c>tipo</c>: consulta | exame | (ambos).</summary>
    [HttpGet("agendamentos")]
    [ProducesResponseType<IEnumerable<AgendamentoResumoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AgendamentoResumoDto>>> Agendamentos(
        [FromQuery] string? tipo, CancellationToken ct) =>
        Ok(await clinico.ListarAgendamentosAsync(PacienteId(), tipo, ct));

    /// <summary>Confirma a presença no exame agendado (card do app).</summary>
    [HttpPost("agendamentos/exames/{solicitacaoExameId:guid}/confirmar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmarExame(Guid solicitacaoExameId, CancellationToken ct)
    {
        await clinico.ConfirmarExameAsync(PacienteId(), solicitacaoExameId, ct);
        return NoContent();
    }

    /// <summary>Avisa que NÃO poderá comparecer (motivo obrigatório). Não cancela o exame —
    /// sinaliza a intenção para a equipe reavaliar a vaga.</summary>
    [HttpPost("agendamentos/exames/{solicitacaoExameId:guid}/cancelar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelarExame(
        Guid solicitacaoExameId, [FromBody] CancelarExameCidadaoRequest corpo, CancellationToken ct)
    {
        await clinico.CancelarExameAsync(PacienteId(), solicitacaoExameId, corpo.Motivo, ct);
        return NoContent();
    }

    public sealed record CancelarExameCidadaoRequest(string Motivo);

    /// <summary>Id do paciente (FHIR) a partir do <c>sub</c>; 403 se o token não for de cidadão.</summary>
    private Guid PacienteId()
    {
        if (User.FindFirstValue("tipo") != "cidadao")
            throw new UnauthorizedAccessException("Token não é de cidadão.");

        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id)
            ? id
            : throw new UnauthorizedAccessException("Token sem identificação de paciente.");
    }

    private Guid? Jti()
    {
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        return Guid.TryParse(jti, out var id) ? id : null;
    }
}
