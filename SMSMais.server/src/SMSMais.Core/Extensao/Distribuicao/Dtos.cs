using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Extensao.Distribuicao;

// ----------------------------------------------------------------------- painel (qualquer logado)

/// <summary>O que há para baixar. Versão nula = ainda não publicado/promovido nesta plataforma.</summary>
public sealed record SituacaoDistribuicaoDto(string? VersaoAtualizador, string? VersaoExtensao);

public enum SituacaoAtivacao
{
    /// <summary>Aguardando alguém logado autorizar.</summary>
    Pendente,
    /// <summary>Autorizada; o computador ainda não buscou o token.</summary>
    Autorizada,
    /// <summary>O computador já buscou o token: está ligado à plataforma.</summary>
    Usada,
    Vencida,
}

public sealed record AtivacaoPendenteDto(
    string CodigoPublico,
    string? Computador,
    DateTime PedidoEm,
    DateTime ExpiraEm,
    SituacaoAtivacao Situacao);

// ------------------------------------------------------------------------------ computador

public sealed record IniciarAtivacaoRequest(string? Computador, string? VersaoAtualizador);

public sealed record AtivacaoIniciadaDto(string Codigo, string UrlAutorizar, int ExpiraEmSegundos, int IntervaloSegundos);

public sealed record TrocarCodigoRequest(string? Codigo, string? Computador, string? VersaoAtualizador);

public enum EstadoDaTroca
{
    Aguardando,
    Autorizado,
    /// <summary>Código desconhecido, vencido ou já usado.</summary>
    Encerrada,
}

public sealed record TrocaDeCodigo(EstadoDaTroca Estado, string? Token = null);

/// <summary>O computador, depois de conferido o token.</summary>
public sealed record DispositivoAutenticado(Guid Id, ExtensaoCanal Canal);

/// <summary>O que o computador diz de si a cada consulta (o inventário da tela).</summary>
public sealed record InventarioDoComputador(string? Instalada, string? Atualizador, string? Chrome);

/// <summary>Resposta de <c>GET /extensao/versao</c>. Os nomes são os que o atualizador lê.</summary>
public sealed record VersaoDaExtensaoDto(string Version, string Canal);

/// <summary>Resposta de <c>GET /extensao/atualizador/versao</c>.</summary>
public sealed record VersaoDoAtualizadorDto(string Versao, string Sha256);

public sealed record ArquivoPublicado(byte[] Conteudo, string ContentType, string NomeArquivo, string Versao);

// --------------------------------------------------------------------------- administração

public sealed record DispositivoDto(
    Guid Id,
    string Computador,
    ExtensaoCanal Canal,
    DateTime AutorizadoEm,
    string? AutorizadoPorNome,
    DateTime? UltimoContatoEm,
    string? VersaoExtensao,
    string? VersaoAtualizador,
    string? SituacaoChrome,
    DateTime? RevogadoEm);

public sealed record DefinirCanalRequest(ExtensaoCanal Canal);

public sealed record PacoteDto(
    Guid Id,
    ExtensaoArtefato Artefato,
    string Versao,
    int Tamanho,
    string Sha256,
    string? Notas,
    DateTime PublicadoEm,
    string? PublicadoPorNome,
    // Publicado/promovido pela API de publicação (com a chave), não por uma pessoa no painel.
    bool PublicadoPelaApi,
    DateTime? PromovidoEm,
    bool PromovidoPelaApi,
    DateTime? RetiradoEm,
    // É esta a versão que os computadores de teste recebem agora.
    bool AtualEmTeste,
    // É esta a versão que todos os computadores recebem agora.
    bool AtualEmProd);

// ------------------------------------------------------------------- API de publicação

/// <summary>Situação da chave da API de publicação. <c>Ativa = false</c>: só se publica pelo painel.</summary>
public sealed record ChavePublicacaoDto(
    bool Ativa,
    string? Prefixo,
    DateTime? CriadaEm,
    string? CriadaPorNome,
    DateTime? UltimoUsoEm);

/// <summary>A chave recém-gerada — o valor em claro só aparece aqui, uma única vez.</summary>
public sealed record ChaveGeradaDto(string Chave, ChavePublicacaoDto Situacao);
