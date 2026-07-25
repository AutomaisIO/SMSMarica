namespace SMSMarica.Core.Integracoes.SisregWeb.Credencial.Dtos;

/// <summary>
/// Estado da credencial SISREG da unidade selecionada. A senha <b>nunca</b> aparece —
/// só o usuário e a confirmação de que existe senha gravada.
/// </summary>
public sealed record SisregCredencialUnidadeDto(
    Guid UnidadeId,
    string UnidadeNome,
    string? UnidadeCnes,
    /// <summary>Usuário do SISREG cadastrado para a unidade (null = nenhuma credencial própria).</summary>
    string? Usuario,
    /// <summary>Existe senha gravada (cifrada) para esta unidade.</summary>
    bool SenhaDefinida,
    /// <summary>CNES que o SISREG confirmou na última autenticação bem-sucedida.</summary>
    string? CnesConfirmado,
    string? UnidadeSisregNome,
    DateTime? ValidadoEm,
    bool Ativo,
    /// <summary>
    /// A unidade não tem credencial própria e está usando a credencial global do store de
    /// Integrações. Funciona, mas o SISREG só devolve a agenda da unidade daquele operador.
    /// </summary>
    bool UsandoFallbackGlobal);

/// <summary>Troca de usuário/senha do SISREG da unidade (fluxo do modal).</summary>
public sealed record SalvarSisregCredencialUnidadeRequest(string Usuario, string Senha);

/// <summary>Resultado de uma autenticação de teste contra o SISREG.</summary>
public sealed record SisregAutenticacaoResultadoDto(
    bool Sucesso,
    string Operador,
    string Perfil,
    string UnidadeSisregNome,
    string? Cnes,
    /// <summary>A unidade da sessão do SISREG confere com a unidade selecionada no sistema.</summary>
    bool UnidadeConfere,
    string Mensagem);
