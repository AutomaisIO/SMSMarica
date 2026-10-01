using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMSMais.Api.Auth;
using SMSMais.Core.Acompanhantes;
using SMSMais.Core.Acompanhantes.Dtos;
using SMSMais.Core.Atendimentos;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Cidadao.Dtos;
using SMSMais.Core.DocumentosPaciente;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Telefones;
using SMSMais.Core.Telefones.Dtos;
using SMSMais.Core.Tratamentos;
using SMSMais.Core.Tratamentos.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Área autenticada do app do cidadão (token <c>tipo=cidadao</c>, single-device).
/// O paciente só enxerga/edita os próprios dados — o id vem do <c>sub</c> do token,
/// nunca do corpo/rota.
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
    ICidadaoClinicoService clinico,
    ITelefoneValidacaoService telefones,
    ITratamentosService transporte,
    IAcompanhantesService acompanhantes) : ControllerBase
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
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AtualizarContato(
        [FromBody] AtualizarContatoCidadaoRequest req, CancellationToken ct)
    {
        // LGPD: sessão aberta em 1 clique pelo link do WhatsApp NÃO troca telefone. O link pode ter
        // chegado a outra pessoa (repasse, celular emprestado) e trocar o número verificado é tomar
        // a conta. Para mexer no contato, entrar com o código (OTP) — que prova o número atual.
        var mexeEmTelefone = req.TelefonePrincipal is not null || req.TelefoneCelular is not null
            || req.TelefoneResidencial is not null;
        if (mexeEmTelefone && string.Equals(User.FindFirst("canal")?.Value, "magic-link", StringComparison.Ordinal))
            throw new SMSMais.Core.Common.Excecoes.ConflitoException(
                "contato.sessao_por_link",
                "Para alterar telefone, entre no aplicativo com o código enviado por WhatsApp.");

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

    // --- Transporte de Pacientes: viagens e acompanhantes do próprio paciente ---

    /// <summary>Próximas viagens do paciente no Transporte de Pacientes (atendimentos ativos).</summary>
    [HttpGet("meus-translados")]
    [ProducesResponseType<IReadOnlyList<ViagemTransporteDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ViagemTransporteDto>> MeusTranslados(CancellationToken ct) =>
        await transporte.ListarProximasViagensAsync(PacienteId(), cancellationToken: ct);

    [HttpGet("me/acompanhantes")]
    [ProducesResponseType<IReadOnlyList<AcompanhanteCidadaoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AcompanhanteCidadaoDto>> MeusAcompanhantes(CancellationToken ct) =>
        [.. (await acompanhantes.ListarDoPacienteAsync(PacienteId(), ct)).Select(AcompanhanteCidadaoDto.De)];

    /// <summary>Confere CPF + nascimento e devolve o nome para o paciente confirmar antes de
    /// cadastrar. Tem cota própria: é consulta de dado de outra pessoa (e a da Receita é paga).</summary>
    [HttpPost("me/acompanhantes/consulta")]
    [EnableRateLimiting("acompanhante-cidadao")]
    [ProducesResponseType<AcompanhanteConsultaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<AcompanhanteConsultaDto> ConsultarAcompanhante(
        [FromBody] ConsultarAcompanhanteRequest req, CancellationToken ct)
    {
        GarantirSessaoPorCodigo();
        return await acompanhantes.ConsultarAsync(PacienteId(), req, ct);
    }

    [HttpPost("me/acompanhantes")]
    [EnableRateLimiting("acompanhante-cidadao")]
    [ProducesResponseType<AcompanhanteCidadaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<AcompanhanteCidadaoDto> AdicionarAcompanhante(
        [FromBody] AdicionarAcompanhanteRequest req, CancellationToken ct)
    {
        GarantirSessaoPorCodigo();
        return AcompanhanteCidadaoDto.De(
            await acompanhantes.AdicionarAsync(PacienteId(), req, OrigemCadastroAcompanhante.App, ct));
    }

    /// <summary>O paciente só tira quem ele mesmo cadastrou pelo app.</summary>
    [HttpDelete("me/acompanhantes/{acompanhanteId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoverAcompanhante(Guid acompanhanteId, CancellationToken ct)
    {
        GarantirSessaoPorCodigo();
        await acompanhantes.RemoverAsync(PacienteId(), acompanhanteId, OrigemCadastroAcompanhante.App, ct);
        return NoContent();
    }

    /// <summary>
    /// Mesma regra da troca de telefone: sessão aberta em 1 clique pelo link do WhatsApp só lê. O
    /// link pode ter chegado a outra pessoa, e quem entra na lista de acompanhantes viaja com o
    /// paciente. Para mexer, entrar com o código enviado por WhatsApp.
    /// </summary>
    private void GarantirSessaoPorCodigo()
    {
        if (string.Equals(User.FindFirst("canal")?.Value, "magic-link", StringComparison.Ordinal))
            throw new SMSMais.Core.Common.Excecoes.ConflitoException(
                "acompanhante.sessao_por_link",
                "Para cadastrar ou tirar acompanhante, entre no aplicativo com o código enviado por WhatsApp.");
    }

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
    private static string DescricaoAtendimento(SMSMais.Core.Atendimentos.Dtos.AtendimentoDto a)
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

    // ---- Documentos (acervo do paciente: o que está no cadastro + o que ele enviou) ----

    /// <summary>
    /// Tudo o que está no cadastro do paciente — documentos, laudos assinados, imagens dos exames —
    /// e o que ele mesmo enviou e ainda espera conferência da equipe.
    /// </summary>
    [HttpGet("documentos")]
    [ProducesResponseType<IReadOnlyList<ItemAcervoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ItemAcervoDto>> Documentos(
        [FromServices] IDocumentosPacienteService acervo, CancellationToken ct) =>
        acervo.ListarAsync(PacienteId(), incluirPendentes: true, ct);

    [HttpGet("documentos/{tipo}/{id:guid}/conteudo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DocumentoConteudo(
        TipoItemAcervo tipo, Guid id, [FromServices] IDocumentosPacienteService acervo, CancellationToken ct)
    {
        var c = await acervo.ObterConteudoAsync(PacienteId(), tipo, id, permitirPendente: true, ct);
        return File(c.Conteudo, c.MimeType, c.NomeArquivo);
    }

    /// <summary>
    /// O paciente anexa um documento (foto ou PDF) pelo app. Entra PENDENTE: só vale no cadastro
    /// depois que alguém da equipe aceita — e há teto de pendentes por paciente.
    /// </summary>
    [HttpPost("documentos")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    [ProducesResponseType<ItemAcervoDto>(StatusCodes.Status200OK)]
    public async Task<ItemAcervoDto> EnviarDocumento(
        IFormFile arquivo, [FromForm] string titulo, [FromForm] string? descricao,
        [FromServices] IDocumentosPacienteService acervo, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await arquivo.CopyToAsync(ms, ct);
        return await acervo.AdicionarAsync(new NovoDocumentoPaciente(
            PacienteId(), titulo, descricao, arquivo.FileName, arquivo.ContentType, ms.ToArray(),
            OrigemDocumentoPaciente.AppCidadao, "app-cidadao", SituacaoDocumentoPaciente.Pendente), ct);
    }

    /// <summary>Retira um documento que o paciente enviou e que ainda não foi conferido.</summary>
    [HttpDelete("documentos/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RetirarDocumento(
        Guid id, [FromServices] IDocumentosPacienteService acervo, CancellationToken ct)
    {
        await acervo.RetirarEnvioDoPacienteAsync(PacienteId(), id, ct);
        return NoContent();
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

    /// <summary>Detalhe completo (ticket) de um exame agendado do paciente.</summary>
    [HttpGet("agendamentos/exames/{solicitacaoExameId:guid}")]
    [ProducesResponseType<AgendamentoExameDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgendamentoExameDetalheDto>> DetalheExame(
        Guid solicitacaoExameId, CancellationToken ct)
    {
        var d = await clinico.ObterExameAgendadoAsync(PacienteId(), solicitacaoExameId, ct);
        return d is null ? NotFound() : d;
    }

    /// <summary>Chave de acesso (confirmação do SISREG) do exame — só no dia do atendimento.
    /// POST porque pode consultar o SISREG na primeira vez e fica na auditoria.</summary>
    [HttpPost("agendamentos/exames/{solicitacaoExameId:guid}/chave-acesso")]
    [ProducesResponseType<ChaveAcessoCidadaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ChaveAcessoCidadaoDto>> ChaveAcessoExame(
        Guid solicitacaoExameId, CancellationToken ct) =>
        Ok(await clinico.ObterChaveAcessoExameAsync(PacienteId(), solicitacaoExameId, ct));

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

    // ---- Troca do celular de contato por OTP (só salva se confirmar o código no número novo) ----

    /// <summary>Envia um código por WhatsApp para o NÚMERO NOVO que a pessoa quer passar a usar.
    /// A troca só se efetiva ao confirmar o código (evita cadastrar um número errado e perder contato).</summary>
    [HttpPost("me/contato/otp")]
    [ProducesResponseType<TelefoneOtpEmitidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<TelefoneOtpEmitidoDto> EnviarOtpContato(
        [FromBody] TrocarContatoOtpRequest req, CancellationToken ct) =>
        await telefones.EnviarCodigoAsync(Cpf(), req.Numero, ct);

    /// <summary>Confirma o código do número novo; em sucesso, ele vira o contato principal validado
    /// (e o telefone principal do cadastro). Só então a troca é salva.</summary>
    [HttpPost("me/contato/confirmar")]
    [ProducesResponseType<TelefoneValidadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<TelefoneValidadoDto> ConfirmarContato(
        [FromBody] ConfirmarContatoRequest req, CancellationToken ct) =>
        await telefones.ConfirmarCodigoAsync(Cpf(), req.Numero, req.Codigo, ct, origem: "pwa-cidadao");

    public sealed record TrocarContatoOtpRequest(string Numero);
    public sealed record ConfirmarContatoRequest(string Numero, string Codigo);

    /// <summary>CPF do cidadão a partir do claim do token (nunca do corpo).</summary>
    private string Cpf()
    {
        var cpf = User.FindFirstValue("cpf");
        return string.IsNullOrWhiteSpace(cpf)
            ? throw new UnauthorizedAccessException("Token sem CPF.")
            : cpf;
    }

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
