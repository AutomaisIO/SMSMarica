namespace Automais.Pabx.Api.Infra.Excecoes;

/// <summary>Dependência da caixa fora do ar ou desligada (AMI, CDR) → 503 (ProblemDetails).</summary>
public sealed class ServicoIndisponivelException(string mensagem) : Exception(mensagem);
