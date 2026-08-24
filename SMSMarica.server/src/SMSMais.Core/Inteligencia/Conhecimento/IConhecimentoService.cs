namespace SMSMais.Core.Inteligencia.Conhecimento;

/// <summary>
/// Sincroniza as bases de conhecimento (.md versionados no repo) com o banco: documentos,
/// chunks e embeddings. Idempotente — chamável no deploy/seed ou sob demanda.
/// </summary>
public interface IConhecimentoService
{
    Task SincronizarAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Resultado da recuperação de contexto (RAG) para uma pergunta contra uma fonte:
/// trechos de conhecimento e aprendizados ativos, já formatados para o provedor de IA.
/// </summary>
public sealed record ContextoRecuperado(string ConhecimentoRecuperado, string AprendizadosAtivos);

/// <summary>
/// Recupera, por similaridade vetorial, os trechos de conhecimento mais relevantes para
/// uma pergunta, somados aos aprendizados ativos da fonte.
/// </summary>
public interface IRecuperadorContexto
{
    Task<ContextoRecuperado> RecuperarAsync(
        Guid fonteId, string pergunta, CancellationToken cancellationToken = default);
}
