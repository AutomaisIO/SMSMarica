using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Worklist;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Pacs;

/// <summary>
/// Monta a <see cref="IdentidadeDicom"/> que um exame DEVERIA ter no PACS.
///
/// <para>Existe para que só haja <b>uma</b> régua de identidade DICOM no sistema. O item de
/// worklist (<see cref="ConstrutorMwlItem"/>) já define como o paciente aparece no equipamento —
/// PatientID = CPF quando há, senão o Guid do hub; nome em "SOBRENOME^NOMES" sem acento. A
/// correção de identidade e a associação reescrevem o objeto e precisam produzir <b>exatamente</b>
/// o mesmo resultado: se divergissem, um estudo corrigido e um estudo novo do mesmo paciente
/// ficariam com identidades diferentes dentro do PACS.</para>
/// </summary>
public interface IResolvedorIdentidadeDicom
{
    /// <summary>Identidade correta do exame (id público do <c>ExameImagem</c>).</summary>
    Task<IdentidadeDicom> ObterAsync(Guid exameImagemId, CancellationToken cancellationToken = default);
}

public sealed class ResolvedorIdentidadeDicom(
    SmsMaisDbContext db, IPacienteResolver pacientes) : IResolvedorIdentidadeDicom
{
    public async Task<IdentidadeDicom> ObterAsync(Guid exameImagemId, CancellationToken cancellationToken = default)
    {
        var exame = await db.ExamesImagem.AsNoTracking()
            .Include(e => e.Solicitacao)
            .FirstOrDefaultAsync(e => e.Id == exameImagemId && e.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException("Exame", exameImagemId.ToString());

        var pacienteId = exame.Solicitacao!.PacienteId;
        if (pacienteId == Guid.Empty)
            throw new ConflitoException("identidade.sem_paciente",
                "O exame não tem paciente vinculado — não há identidade para gravar no DICOM.");

        var paciente = await pacientes.ResolverAsync(pacienteId, cancellationToken)
            ?? throw new NaoEncontradoException("Paciente", pacienteId.ToString());

        // Mesma regra do worklist: CPF quando válido, senão o Guid do hub.
        var cpf = new string((paciente.Cpf ?? string.Empty).Where(char.IsDigit).ToArray());
        var patientId = cpf.Length == 11 ? cpf : pacienteId.ToString();

        return new IdentidadeDicom(
            PatientId: patientId,
            PatientName: ConstrutorMwlItem.FormatarPn(paciente.Nome),
            AccessionNumber: exame.AccessionNumber ?? string.Empty,
            DataNascimento: paciente.DataNascimento,
            Sexo: MapearSexo(paciente.Sexo),
            IssuerOfPatientId: ConstrutorMwlItem.IssuerDoPatientId(patientId));
    }

    private static string? MapearSexo(Sexo sexo) => sexo switch
    {
        Sexo.Masculino => "M",
        Sexo.Feminino => "F",
        _ => null,
    };
}
