using Hl7.Fhir.Model;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Core.Pacientes.Fhir;

namespace SMSMarica.Core.Pacientes;

/// <summary>
/// Pacientes agora vivem APENAS no hub FHIR (Automais.Fhir). Este serviço é um
/// proxy: consultar/cadastrar/atualizar paciente vira chamada à API FHIR
/// (ADR-0010 + regra "FHIR é API-only; smsmarica é o consumidor").
/// </summary>
public sealed class PacientesService(IPacienteFhirClient fhir) : IPacientesService
{
    private const int LimiteBusca = 10;

    public async Task<IReadOnlyList<PacienteListItemDto>> BuscarAsync(
        string? termo, CancellationToken cancellationToken = default)
    {
        Hl7.Fhir.Model.Bundle bundle;

        if (string.IsNullOrWhiteSpace(termo))
        {
            bundle = await fhir.BuscarAsync(ct: cancellationToken);
        }
        else
        {
            termo = termo.Trim();
            var digitos = Digitos(termo);
            var soDigitos = digitos.Length == termo.Replace(".", "").Replace("-", "").Replace(" ", "").Length;

            bundle = digitos.Length >= 3 && digitos.Length <= 11 && soDigitos
                ? await fhir.BuscarAsync(identifier: digitos, ct: cancellationToken)
                : await fhir.BuscarAsync(name: termo, ct: cancellationToken);
        }

        return [.. bundle.Entry
            .Select(e => e.Resource)
            .OfType<Hl7.Fhir.Model.Patient>()
            .Take(LimiteBusca)
            .Select(PacienteFhirMapper.ParaListItem)];
    }

    public async Task<PacienteDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var patient = await fhir.ObterAsync(id, cancellationToken)
            ?? throw new NaoEncontradoException("Paciente", id);
        return PacienteFhirMapper.ParaDto(patient);
    }

    public async Task<PacienteExistenciaDto?> ObterPorCpfAsync(string cpf, CancellationToken cancellationToken = default)
    {
        var normalizado = Digitos(cpf);
        if (normalizado.Length != 11) return null;

        var bundle = await fhir.BuscarAsync(identifier: normalizado, ct: cancellationToken);
        var patient = bundle.Entry.Select(e => e.Resource).OfType<Hl7.Fhir.Model.Patient>().FirstOrDefault();
        if (patient is null) return null;

        var dto = PacienteFhirMapper.ParaDto(patient);
        return new PacienteExistenciaDto(dto.Id, dto.NomeCompleto, dto.Cpf, dto.Ativo);
    }

    public async Task<Guid> CadastrarAsync(CadastrarPacienteRequest request, CancellationToken cancellationToken = default)
    {
        var cpf = Digitos(request.Cpf);

        var existentes = await fhir.BuscarAsync(identifier: cpf, ct: cancellationToken);
        if (existentes.Entry.Select(e => e.Resource).OfType<Hl7.Fhir.Model.Patient>().Any())
            throw new ConflitoException("paciente.cpf_duplicado", "Já existe paciente com este CPF no hub FHIR.");

        var patient = PacienteFhirMapper.ConstruirNovo(request);
        var criado = await fhir.CriarAsync(patient, cancellationToken);
        return Guid.Parse(criado.Id!);
    }

    public Task<Guid> PromoverAsync(PromoverPacienteRequest request, CancellationToken cancellationToken = default) =>
        throw new ConflitoException(
            "paciente.promover_descontinuado",
            "Promover usuário a paciente foi descontinuado: paciente é recurso do hub FHIR (use POST /pacientes).");

    public async Task AtualizarAsync(Guid id, AtualizarPacienteRequest request, CancellationToken cancellationToken = default)
    {
        var patient = await fhir.ObterAsync(id, cancellationToken)
            ?? throw new NaoEncontradoException("Paciente", id);

        PacienteFhirMapper.AplicarAtualizacao(patient, request);
        await fhir.AtualizarAsync(id, patient, cancellationToken);
    }

    public async Task AdicionarTelefoneAsync(
        Guid id, AdicionarTelefoneRequest request, CancellationToken cancellationToken = default)
    {
        var numero = request.Numero?.Trim() ?? string.Empty;
        if (numero.Length == 0)
            throw new ValidacaoException("paciente.telefone_obrigatorio", "Informe o número do telefone.");

        var patient = await fhir.ObterAsync(id, cancellationToken)
            ?? throw new NaoEncontradoException("Paciente", id);

        // Append em Patient.telecom nativo, sem tocar nos demais dados. Idempotente:
        // se o mesmo número (comparando só dígitos) já estiver lá, não duplica.
        var alvo = Digitos(numero);
        patient.Telecom ??= [];
        var jaExiste = alvo.Length > 0 && patient.Telecom.Any(t =>
            t.System == ContactPoint.ContactPointSystem.Phone && Digitos(t.Value) == alvo);
        if (jaExiste) return;

        patient.Telecom.Add(new ContactPoint
        {
            System = ContactPoint.ContactPointSystem.Phone,
            Value = numero,
            Use = MapearUso(request.Tipo),
        });

        await fhir.AtualizarAsync(id, patient, cancellationToken);
    }

    private static ContactPoint.ContactPointUse MapearUso(string? tipo) => tipo?.Trim().ToLowerInvariant() switch
    {
        "residencial" or "casa" or "home" => ContactPoint.ContactPointUse.Home,
        "comercial" or "trabalho" or "work" => ContactPoint.ContactPointUse.Work,
        _ => ContactPoint.ContactPointUse.Mobile,
    };

    public Task DesativarAsync(Guid id, CancellationToken cancellationToken = default) =>
        fhir.ExcluirAsync(id, cancellationToken);

    public Task ReativarAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new ConflitoException(
            "paciente.reativar_nao_suportado",
            "Reativação não está disponível nesta fase (o hub FHIR ainda não expõe undelete).");

    private static string Digitos(string? valor) =>
        string.IsNullOrEmpty(valor) ? string.Empty : new([.. valor.Where(char.IsDigit)]);
}
