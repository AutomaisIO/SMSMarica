using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Medicos.Dtos;
using SMSMarica.Core.Medicos.Fhir;
using SMSMarica.Data;

namespace SMSMarica.Core.Medicos;

/// <summary>
/// Médicos agora vivem APENAS no hub FHIR como Practitioner (ADR-0010 + régua
/// "identidade clínica → FHIR"). Este serviço é proxy do /fhir/Practitioner.
/// O Usuario (login) é vinculado por CPF — resolvido aqui, não pelo id FHIR.
/// </summary>
public sealed class MedicosService(IPractitionerFhirClient fhir, SmsMaricaDbContext db) : IMedicosService
{
    private const int LimiteBusca = 10;

    public async Task<IReadOnlyList<MedicoListItemDto>> BuscarAsync(
        string? termo,
        string? conselho = null,
        string? conselhoExceto = null,
        CancellationToken cancellationToken = default)
    {
        Hl7.Fhir.Model.Bundle bundle;

        if (string.IsNullOrWhiteSpace(termo))
        {
            // Sem termo: backend devolve os últimos cadastros (LastUpdated desc).
            bundle = await fhir.BuscarAsync(conselho: conselho, conselhoNe: conselhoExceto, ct: cancellationToken);
        }
        else
        {
            termo = termo.Trim();
            var digitos = Digitos(termo);
            var soDigitos = digitos.Length == termo.Replace(".", "").Replace("-", "").Replace(" ", "").Length;

            // 3..11 dígitos "puros" → busca por identifier (CPF); senão por nome.
            bundle = digitos.Length >= 3 && digitos.Length <= 11 && soDigitos
                ? await fhir.BuscarAsync(identifier: digitos, conselho: conselho, conselhoNe: conselhoExceto, ct: cancellationToken)
                : await fhir.BuscarAsync(name: termo, conselho: conselho, conselhoNe: conselhoExceto, ct: cancellationToken);
        }

        return [.. bundle.Entry.Select(e => e.Resource).OfType<Hl7.Fhir.Model.Practitioner>()
            .Take(LimiteBusca)
            .Select(MedicoFhirMapper.ParaListItem)];
    }

    private static string Digitos(string? valor) =>
        string.IsNullOrEmpty(valor) ? string.Empty : new([.. valor.Where(char.IsDigit)]);

    public async Task<MedicoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var p = await fhir.ObterAsync(id, cancellationToken)
            ?? throw new NaoEncontradoException("Medico", id);

        var dto = MedicoFhirMapper.ParaDto(p);

        // Resolve o Usuario (login) pelo CPF — é por aí que médico↔usuário se ligam
        // (não pelo id FHIR). Null = médico sem usuário de acesso.
        var cpf = Digitos(dto.Cpf);
        Guid? usuarioId = null;
        bool? usuarioAtivo = null;
        if (cpf.Length == 11)
        {
            var u = await db.Usuarios.AsNoTracking()
                .Where(x => x.Cpf == cpf && x.ExcluidoEm == null)
                .Select(x => new { x.Id, x.Ativo })
                .FirstOrDefaultAsync(cancellationToken);
            if (u is not null)
            {
                usuarioId = u.Id;
                usuarioAtivo = u.Ativo;
            }
        }

        return dto with { UsuarioId = usuarioId, UsuarioAtivo = usuarioAtivo ?? dto.UsuarioAtivo };
    }

    public async Task<Guid> CadastrarAsync(CadastrarMedicoRequest request, CancellationToken cancellationToken = default)
    {
        var conselho = (request.Conselho ?? "CRM").Trim().ToUpperInvariant();
        var registro = new string([.. (request.Registro ?? string.Empty).Where(char.IsDigit)]);
        var uf = (request.UfConselho ?? string.Empty).Trim().ToUpperInvariant();

        var existentes = await fhir.BuscarAsync(identifier: $"{MedicoFhirMapper.SystemConselho(conselho, uf)}|{registro}", ct: cancellationToken);
        if (existentes.Entry.Select(e => e.Resource).OfType<Hl7.Fhir.Model.Practitioner>().Any())
            throw new ConflitoException("medico.registro_duplicado", $"Já existe profissional com {conselho} {registro}/{uf}.");

        var practitioner = MedicoFhirMapper.ConstruirNovo(request);
        var criado = await fhir.CriarAsync(practitioner, cancellationToken);
        return Guid.Parse(criado.Id!);
    }

    public Task<Guid> PromoverAsync(PromoverMedicoRequest request, CancellationToken cancellationToken = default) =>
        throw new ConflitoException(
            "medico.promover_descontinuado",
            "Promover usuário a médico foi descontinuado: médico é Practitioner do hub FHIR (use POST /medicos).");

    public async Task AtualizarAsync(Guid id, AtualizarMedicoRequest request, CancellationToken cancellationToken = default)
    {
        var p = await fhir.ObterAsync(id, cancellationToken)
            ?? throw new NaoEncontradoException("Medico", id);

        MedicoFhirMapper.AplicarAtualizacao(p, request);
        await fhir.AtualizarAsync(id, p, cancellationToken);
    }

    public Task DesativarAsync(Guid id, CancellationToken cancellationToken = default) =>
        fhir.ExcluirAsync(id, cancellationToken);
}
