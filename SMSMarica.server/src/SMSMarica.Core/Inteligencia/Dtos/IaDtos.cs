namespace SMSMarica.Core.Inteligencia.Dtos;

// ---- Perguntar ----

/// <summary>Pergunta do usuário + bases-alvo selecionadas (multi-seleção).</summary>
public sealed record PerguntarRequest(string Pergunta, IReadOnlyList<Guid> FonteIds);

/// <summary>Resposta agregada: uma entrada por base selecionada.</summary>
public sealed record PerguntarRespostaDto(IReadOnlyList<RespostaIaDto> Respostas);

/// <summary>Resposta abstraída para o usuário leigo: resumo + dados + plano de visualização.</summary>
public sealed record RespostaIaDto(
    Guid FonteId,
    string FonteNome,
    string Status,
    string? Resumo,
    string? Visualizacao,
    string? Titulo,
    IReadOnlyList<string> Colunas,
    IReadOnlyList<IReadOnlyList<object?>> Dados,
    string? Sql,
    Guid ConsultaId,
    string? Erro);

/// <summary>Item do dropdown de bases na tela de perguntar.</summary>
public sealed record FonteResumoDto(Guid Id, string Nome, string Tipo, string Ambiente);

// ---- Configuração ----

public sealed record ConfiguracaoDto(string Provedor, string Modelo, string ProvedorEmbeddings,
    string ModeloEmbeddings, bool TokenDefinido, bool TokenEmbeddingsDefinido);

public sealed record AtualizarConfiguracaoRequest(string Provedor, string Modelo,
    string? Token, string ProvedorEmbeddings, string ModeloEmbeddings, string? TokenEmbeddings);

// ---- Bases de dados ----

public sealed record FonteDetalheDto(Guid Id, string Nome, string Tipo, string Dialeto,
    string Ambiente, string? Host, int? Porta, string? Servico, string? Usuario,
    string? BaseUrl, bool SenhaDefinida, bool Ativo);

public sealed record CadastrarFonteRequest(string Nome, string Tipo, string Dialeto, string Ambiente,
    string? Host, int? Porta, string? Servico, string? Usuario, string? Senha, string? BaseUrl);

public sealed record AtualizarFonteRequest(string Nome, string Ambiente, string? Host, int? Porta,
    string? Servico, string? Usuario, string? Senha, string? BaseUrl, bool Ativo);

public sealed record TestarConexaoResultado(bool Sucesso, string? Mensagem);
