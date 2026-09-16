namespace SMSMais.Core.Inteligencia.Dtos;

/// <summary>Base ativa (para a Consulta Inteligente e a configuração). <c>Slug</c> mapeia a base.</summary>
public sealed record FonteResumoDto(Guid Id, string Nome, string Tipo, string Ambiente, string Slug);

// ---- Configuração ----

public sealed record ConfiguracaoDto(string Provedor, string Modelo, string ProvedorEmbeddings,
    string ModeloEmbeddings, bool TokenDefinido, bool TokenEmbeddingsDefinido);

public sealed record AtualizarConfiguracaoRequest(string Provedor, string Modelo,
    string? Token, string ProvedorEmbeddings, string ModeloEmbeddings, string? TokenEmbeddings);

// ---- Bases de dados ----

public sealed record FonteDetalheDto(Guid Id, string Nome, string? Slug, string Tipo, string Dialeto,
    string Ambiente, string? Host, int? Porta, string? Servico, string? Usuario,
    string? BaseUrl, bool SenhaDefinida, bool Ativo, bool ViaAgente, bool AgenteConectado, bool TokenDefinido,
    string? Familia = null, string? ParametrosJson = null);

public sealed record CadastrarFonteRequest(string Nome, string? Slug, string Tipo, string Dialeto, string Ambiente,
    string? Host, int? Porta, string? Servico, string? Usuario, string? Senha, string? BaseUrl, bool ViaAgente = false,
    string? Familia = null, string? ParametrosJson = null);

public sealed record AtualizarFonteRequest(string Nome, string? Slug, string Ambiente, string? Host, int? Porta,
    string? Servico, string? Usuario, string? Senha, string? BaseUrl, bool Ativo,
    string? Familia = null, string? ParametrosJson = null);

public sealed record TestarConexaoResultado(bool Sucesso, string? Mensagem);

/// <summary>Token do agente recém-gerado. É mostrado UMA vez — guardamos só o hash.</summary>
public sealed record TokenAgenteGerado(string Slug, string Token, string WssUrl);

/// <summary>O agente se auto-provisiona pelo slug (primeiro run, autenticado com o login do admin).</summary>
public sealed record ProvisionarAgenteRequest(string Slug, string? Nome);
