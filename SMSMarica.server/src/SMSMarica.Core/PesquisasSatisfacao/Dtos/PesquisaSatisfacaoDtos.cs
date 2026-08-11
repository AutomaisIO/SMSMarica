namespace SMSMarica.Core.PesquisasSatisfacao.Dtos;

/// <summary>Resultado do disparo manual, para a tela da retaguarda mostrar o que aconteceu.</summary>
public sealed record EnvioPesquisaDto(Guid PesquisaId, string Url, bool JaEnviadaAntes);
