using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Conversas.RespostasRapidas.Dtos;

/// <summary>Uma variável manual da mensagem — o operador preenche ao usar o atalho.</summary>
public sealed record RespostaRapidaCampoDto(
    string Nome,
    string? Rotulo,
    TipoCampoRespostaRapida Tipo,
    int Ordem);

/// <summary>Mensagem pronta do chat, com as variáveis que ela pede.</summary>
/// <param name="UnidadeId"><c>null</c> = vale para toda a SMS.</param>
/// <param name="TagsAutomaticas">
/// Tags do corpo que o servidor resolve sozinho (primeironome, saudacao…) — a UI mostra, mas
/// não pede.
/// </param>
public sealed record RespostaRapidaDto(
    Guid Id,
    string Titulo,
    string Corpo,
    string? Categoria,
    Guid? UnidadeId,
    string? UnidadeNome,
    bool Ativo,
    int Ordem,
    IReadOnlyList<RespostaRapidaCampoDto> Campos,
    IReadOnlyList<string> TagsAutomaticas);

public sealed record SalvarRespostaRapidaRequest(
    string Titulo,
    string Corpo,
    string? Categoria,
    Guid? UnidadeId,
    bool Ativo,
    int Ordem,
    IReadOnlyList<RespostaRapidaCampoDto> Campos);

/// <summary>Valores digitados pelo operador (chave = nome do campo manual).</summary>
public sealed record ResolverRespostaRapidaRequest(IReadOnlyDictionary<string, string> Valores);

/// <summary>Texto pronto para cair no campo de digitação do operador.</summary>
/// <param name="Pendentes">Tags que ficaram sem valor — a UI avisa antes de enviar.</param>
public sealed record TextoResolvidoDto(string Texto, IReadOnlyList<string> Pendentes);

/// <summary>Catálogo das tags automáticas, para a tela de cadastro.</summary>
public sealed record TagAutomaticaDto(string Nome, string Descricao);
