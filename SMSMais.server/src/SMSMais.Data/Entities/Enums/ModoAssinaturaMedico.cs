namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Como o médico oficializa o laudo (ADR-0061). Configurado pelo administrador no cadastro
/// do médico, na mesma aba da rubrica. Sem configuração = <see cref="SemCertificado"/>
/// ("login e senha"): decisão de 24/09/2026 — a rede quase toda não tem certificado, e quem
/// tem (a Dra. Claudia, única no assinador do computador) ficou gravada explicitamente.
/// </summary>
public enum ModoAssinaturaMedico
{
    /// <summary>
    /// Certificado na loja do Windows (VIDaaS Connect, token A3, A1), assinado pelo agente
    /// local <c>Automais.Assinador.Agente</c> lançado por protocolo (ADR-0015).
    /// </summary>
    Desktop = 1,

    /// <summary>
    /// Certificado em nuvem VIDaaS, assinado pela API IntegraICP: o médico autoriza no app
    /// do celular e o servidor assina o hash sem nada instalado na máquina.
    /// </summary>
    Nuvem = 2,

    /// <summary>
    /// Médico sem certificado digital: o laudo sai com o carimbo (rubrica + nome/CRM) e o
    /// selo de verificação por QR Code, mas SEM assinatura ICP-Brasil. O próprio PDF declara
    /// isso no rodapé.
    /// </summary>
    SemCertificado = 3,
}
