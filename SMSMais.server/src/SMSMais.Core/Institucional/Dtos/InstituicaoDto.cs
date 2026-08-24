using SMSMais.Core.Common.Dtos;

namespace SMSMais.Core.Institucional.Dtos;

/// <summary>
/// Identidade da instituição desta instância (ADR-0043), como o painel e os PWAs a consomem.
/// <b>Tudo aqui é público</b> — o DTO é servido sem autenticação em
/// <c>GET /publico/instituicao</c>, porque o front precisa se pintar antes de existir login.
/// Nenhum campo sensível pode entrar.
/// </summary>
public sealed record InstituicaoDto(
    string Nome,
    string NomeSecretaria,
    string NomeCurto,
    string? Sigla,
    string? Cnpj,
    string? CodigoIbge,
    string Uf,
    int? DddPadrao,
    EnderecoDto? Endereco,
    string? Telefone,
    string? EmailContato,
    string? EmailDpo,
    string? WhatsAppNumeroPublico,
    Guid? LogoMidiaId,
    Guid? FaviconMidiaId,
    string? CorPrimaria,
    string? CorSecundaria,
    string? CorGradienteInicio,
    string? CorGradienteFim,
    string? UrlPainel,
    string? UrlApp,
    string? UrlArquivos,
    string? AssinaturaProdutoHtml,
    DateTime? AtualizadoEm);

/// <summary>Corpo de escrita da identidade institucional (tela Sistema → Instituição).</summary>
public sealed record SalvarInstituicaoRequest(
    string Nome,
    string NomeSecretaria,
    string NomeCurto,
    string? Sigla,
    string? Cnpj,
    string? CodigoIbge,
    string Uf,
    int? DddPadrao,
    EnderecoDto? Endereco,
    string? Telefone,
    string? EmailContato,
    string? EmailDpo,
    string? WhatsAppNumeroPublico,
    Guid? LogoMidiaId,
    Guid? FaviconMidiaId,
    string? CorPrimaria,
    string? CorSecundaria,
    string? CorGradienteInicio,
    string? CorGradienteFim,
    string? UrlPainel,
    string? UrlApp,
    string? UrlArquivos,
    string? AssinaturaProdutoHtml);
