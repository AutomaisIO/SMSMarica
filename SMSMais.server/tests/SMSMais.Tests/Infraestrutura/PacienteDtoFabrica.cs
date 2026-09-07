using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Infraestrutura;

/// <summary>
/// Monta um <see cref="PacienteDto"/> mínimo para dublê.
///
/// <para>O DTO é o retrato completo do paciente FHIR — 37 campos posicionais obrigatórios,
/// incluindo endereço, alergias e foto. Nos testes que só precisam de nome/CPF/CNS, construí-lo
/// à mão viraria uma parede de argumentos repetida em cada arquivo.</para>
/// </summary>
public static class PacienteDtoFabrica
{
    public static PacienteDto Criar(
        Guid id,
        string nome,
        string? cpf = null,
        string? cns = null,
        DateOnly? nascimento = null,
        // Parâmetro opcional (07/09/2026): as regras de elegibilidade da regulação avaliam sexo,
        // e o valor fixo `NaoInformado` deixava esse caminho sem teste.
        Sexo sexo = Sexo.NaoInformado) =>
        new(
            Id: id,
            NomeCompleto: nome,
            Cpf: cpf ?? string.Empty,
            Cns: cns,
            Latitude: 0,
            Longitude: 0,
            Ativo: true,
            CadastradoEm: DateTime.UtcNow,
            Rg: null,
            DataNascimento: nascimento ?? new DateOnly(1980, 1, 1),
            Sexo: sexo,
            EstadoCivil: EstadoCivil.NaoInformado,
            RacaCor: RacaCor.NaoInformado,
            Escolaridade: Escolaridade.NaoInformado,
            Ocupacao: null,
            Naturalidade: null,
            Nacionalidade: "Brasileira",
            NomeDaMae: null,
            NomeDoPai: null,
            ResponsavelLegal: null,
            Endereco: null,
            TelefonePrincipal: null,
            TelefoneCelular: null,
            TelefoneResidencial: null,
            Email: null,
            ContatoEmergencia: null,
            AlturaCm: null,
            PesoKg: null,
            TipoSanguineo: TipoSanguineo.NaoInformado,
            FatorRh: FatorRh.NaoInformado,
            Alergias: [],
            MedicamentosContinuos: [],
            Comorbidades: [],
            Deficiencias: [],
            PlanoSaude: null,
            Observacoes: null,
            FotoBase64: null);
}
