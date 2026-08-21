namespace Automais.Zap.Core.Legal;

/// <summary>
/// Dados institucionais das páginas públicas de privacidade, termos e exclusão de dados.
///
/// Vêm de configuração, não de código: as URLs são do App da Meta, que é da Automais e serve
/// N prefeituras — cravar razão social ou e-mail no fonte repetiria o erro que o ADR-0043
/// mandou não repetir.
/// </summary>
public sealed class LegalOptions
{
    public const string Secao = "Legal";

    public string NomeFantasia { get; set; } = "Automais";
    public string Plataforma { get; set; } = "SMSMais";
    public string? RazaoSocial { get; set; }
    public string? Cnpj { get; set; }

    /// <summary>Endereço para exercício de direitos do titular (LGPD art. 18).</summary>
    public string EmailContato { get; set; } = "";

    /// <summary>Data da última revisão do texto, exibida no rodapé das páginas.</summary>
    public string AtualizadoEm { get; set; } = "21 de agosto de 2026";
}
