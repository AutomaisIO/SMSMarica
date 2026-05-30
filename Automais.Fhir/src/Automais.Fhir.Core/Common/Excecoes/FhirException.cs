namespace Automais.Fhir.Core.Common.Excecoes;

/// <summary>Base das exceções de domínio do serviço FHIR. Mapeadas para
/// <c>OperationOutcome</c> + status HTTP pelo middleware da Api.</summary>
public abstract class FhirException(string mensagem) : Exception(mensagem)
{
    /// <summary>Código do issue do OperationOutcome (ex.: not-found, invalid).</summary>
    public abstract string IssueCode { get; }

    /// <summary>Status HTTP correspondente.</summary>
    public abstract int StatusHttp { get; }
}

/// <summary>Recurso não existe (ou está logicamente excluído). HTTP 404.</summary>
public sealed class RecursoNaoEncontradoException(string tipo, string id)
    : FhirException($"{tipo}/{id} não encontrado.")
{
    public override string IssueCode => "not-found";
    public override int StatusHttp => 404;
}

/// <summary>Recurso inválido / não-conforme ao FHIR. HTTP 400.</summary>
public sealed class RecursoInvalidoException(string mensagem)
    : FhirException(mensagem)
{
    public override string IssueCode => "invalid";
    public override int StatusHttp => 400;
}
