using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Faturamento;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Tratamentos;
using SMSMais.Core.Tratamentos.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Tratamentos;

/// <summary>Monta o mínimo do Transporte de Pacientes para os testes: tipo, destino e atendimento.</summary>
internal static class TransporteFabrica
{
    // Bits do DayOfWeek: 0 = domingo … 6 = sábado.
    public const int Segunda = 1 << 1;
    public const int Terca = 1 << 2;
    public const int Quarta = 1 << 3;
    public const int Quinta = 1 << 4;
    public const int Sexta = 1 << 5;

    public static readonly NecessidadesRequest SemNecessidades =
        new(MobilidadeTransporte.Independente, false, false, false, false, null);

    public static readonly RegraAcompanhantesRequest UmAcompanhante = new(1, null);

    /// <summary>A próxima <paramref name="dia"/> depois de hoje (Brasília) — datas que valem sempre.</summary>
    public static DateOnly Proxima(DayOfWeek dia, int semanasDepois = 0)
    {
        var data = FusoBrasilia.HojeEmBrasilia().AddDays(1);
        while (data.DayOfWeek != dia) data = data.AddDays(1);
        return data.AddDays(7 * semanasDepois);
    }

    public static async Task<Guid> NovoTipoAsync(SmsMaisDbContext db, int? tempoMedio = 240)
    {
        var tipo = new TipoTratamento
        {
            Id = Guid.CreateVersion7(),
            Nome = $"Tipo {Guid.NewGuid():N}",
            Codigo = $"tipo_{Guid.NewGuid():N}",
            TempoMedioMinutos = tempoMedio,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.TiposTratamento.Add(tipo);
        await db.SaveChangesAsync();
        return tipo.Id;
    }

    public static async Task<Guid> NovoDestinoAsync(SmsMaisDbContext db)
    {
        var destino = new UnidadeAtendimento
        {
            Id = Guid.CreateVersion7(),
            Nome = $"DESTINO {Guid.NewGuid():N}".ToUpperInvariant(),
            Gps = new Gps(-22.9, -43.1),
            Externa = true,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.UnidadesAtendimento.Add(destino);
        await db.SaveChangesAsync();
        return destino.Id;
    }

    public static TratamentosService Servico(SmsMaisDbContext db, IPacientesService? pacientes = null, Guid? usuarioId = null) =>
        new(db,
            Substitute.For<IPacienteResolver>(),
            Substitute.For<IFaturamentoService>(),
            pacientes ?? Substitute.For<IPacientesService>(),
            new UsuarioAtualAccessorFake(usuarioId),
            NullLogger<TratamentosService>.Instance);

    public static CadastrarTratamentoRequest Novo(
        Guid destinoId,
        Guid tipoId,
        AgendaRequest? agenda = null,
        RegraAcompanhantesRequest? acompanhantes = null,
        Guid? pacienteId = null) => new(
        pacienteId ?? Guid.NewGuid(),
        destinoId,
        tipoId,
        "Hemodiálise",
        null,
        agenda ?? new AgendaRequest(Proxima(DayOfWeek.Monday), Segunda, 1, false),
        SemNecessidades,
        acompanhantes ?? UmAcompanhante);
}
