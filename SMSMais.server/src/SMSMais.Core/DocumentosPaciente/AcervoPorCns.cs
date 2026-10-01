using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Pacientes;

namespace SMSMais.Core.DocumentosPaciente;

/// <summary>
/// Os rascunhos do SER e do SERNIT só conhecem o paciente pelo CNS digitado. Aqui o CNS vira o
/// paciente do hub para que esses formulários também leiam e alimentem o acervo.
/// </summary>
internal static class AcervoPorCns
{
    public static async Task<Guid?> ResolverPacienteAsync(
        IPacientesService? pacientes, string? cns, CancellationToken ct)
    {
        var digitos = new string([.. (cns ?? string.Empty).Where(char.IsDigit)]);
        if (pacientes is null || digitos.Length != 15) return null;
        var p = await pacientes.ObterPorCnsAsync(digitos, ct);
        return p?.Id;
    }

    public static async Task<Guid> ExigirPacienteAsync(
        IPacientesService? pacientes, string? cns, CancellationToken ct) =>
        await ResolverPacienteAsync(pacientes, cns, ct)
        ?? throw new ValidacaoException(
            "acervo.sem_paciente",
            "Informe um CNS de paciente cadastrado para usar os documentos do cadastro.");
}
