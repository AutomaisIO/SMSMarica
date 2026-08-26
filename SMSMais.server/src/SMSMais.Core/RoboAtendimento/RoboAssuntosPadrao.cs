namespace SMSMais.Core.RoboAtendimento;

/// <summary>
/// Ids fixos de assuntos padrão que o CÓDIGO precisa referenciar (o restante fica no seed).
/// Hoje só o "Verificação cadastral", usado pela classificação por estado (aguardando a
/// resposta do desafio <c>validacao_cadastro</c>).
/// </summary>
public static class RoboAssuntosPadrao
{
    public static readonly Guid VerificacaoCadastralId = new("0b0b0b0b-0000-0000-0000-0000000000eb");
}
