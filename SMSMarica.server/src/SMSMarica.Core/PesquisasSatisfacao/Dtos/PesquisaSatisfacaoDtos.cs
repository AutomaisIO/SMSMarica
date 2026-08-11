namespace SMSMarica.Core.PesquisasSatisfacao.Dtos;

/// <summary>
/// O que a tela pública precisa saber antes de desenhar o formulário. Não devolve nada clínico:
/// o token abre a pesquisa, não o prontuário.
/// </summary>
public sealed record PesquisaPublicaDto(
    string? Unidade,
    DateTime AtendimentoEm,
    DateTime ExpiraEm,
    bool Expirada,
    bool JaRespondida,
    string InstrumentoVersao);

/// <summary>Respostas enviadas pelo cidadão: id da pergunta → alternativa escolhida.</summary>
public sealed record ResponderPesquisaRequest(Dictionary<string, string> Respostas);

/// <summary>Resultado do disparo manual, para a tela da retaguarda mostrar o que aconteceu.</summary>
public sealed record EnvioPesquisaDto(Guid PesquisaId, string Url, bool JaEnviadaAntes);
