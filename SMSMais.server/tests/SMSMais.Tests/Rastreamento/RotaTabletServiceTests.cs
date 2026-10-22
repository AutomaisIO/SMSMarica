using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Common.Dtos;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Geo;
using SMSMais.Core.Geo.Google;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Rastreamento.Dispositivos;
using SMSMais.Core.Translado.Geracao;
using SMSMais.Core.Translado.Geracao.Dtos;
using SMSMais.Core.Translado.Geracao.IA;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Rastreamento;

/// <summary>
/// Rota do dia no tablet do veículo: o que a central montou em Translados, com Atendimentos, Unidades
/// de Atendimento e Tipos de tratamento, chega ao tablet como a navegação precisa — coleta na casa de
/// cada paciente na ordem da rota, destino em cada unidade, passageiros com acompanhantes, assentos e
/// condição. Pacientes fictícios (nada de dado real).
/// </summary>
[Collection(nameof(PostgresCollection))]
public partial class RotaTabletServiceTests(PostgresFixture fixture)
{
    private readonly IPacienteResolver _resolver = Substitute.For<IPacienteResolver>();
    private readonly IPacientesService _pacientes = Substitute.For<IPacientesService>();
    private readonly IGeocodificadorService _geo = Substitute.For<IGeocodificadorService>();

    private RotaTabletService Servico(SmsMaisDbContext db) =>
        new(db, _resolver, _pacientes, _geo, NullLogger<RotaTabletService>.Instance);

    private sealed record Frota(DispositivoVeiculo Tablet, Guid MotoristaId, List<Assento> Assentos);

    private static async Task<Frota> VeiculoComTabletAsync(SmsMaisDbContext db)
    {
        var agora = DateTime.UtcNow;
        var veiculo = new Veiculo
        {
            Id = Guid.NewGuid(), Placa = $"R{Random.Shared.Next(100000, 999999)}",
            Modelo = "Spin", Fabricante = "Chevrolet", Cor = "Branca", CriadoEm = agora,
        };
        var fileira = new Fileira { Id = Guid.NewGuid(), VeiculoId = veiculo.Id, Ordem = 1, CriadoEm = agora };
        var assentos = Enumerable.Range(1, 4).Select(n => new Assento
        {
            Id = Guid.NewGuid(), FileiraId = fileira.Id, Numero = n, CriadoEm = agora,
            Tipo = n == 4 ? TipoAssento.Acompanhante : TipoAssento.Passageiro,
        }).ToList();
        var tablet = new DispositivoVeiculo
        {
            Id = Guid.NewGuid(), VeiculoId = veiculo.Id, TokenHash = Guid.NewGuid().ToString("N"),
            AtivadoEm = agora, CriadoEm = agora,
        };
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(), NomeCompleto = "Motorista Simulado", SenhaHash = "PENDENTE_AUTH", CriadoEm = agora,
        };
        var motorista = new Motorista { Id = Guid.NewGuid(), UsuarioId = usuario.Id, Cnh = Guid.NewGuid().ToString("N")[..11], CriadoEm = agora };
        db.Veiculos.Add(veiculo);
        db.Fileiras.Add(fileira);
        db.Assentos.AddRange(assentos);
        db.DispositivosVeiculo.Add(tablet);
        db.Usuarios.Add(usuario);
        db.Motoristas.Add(motorista);
        await db.SaveChangesAsync();
        return new Frota(tablet, motorista.Id, assentos);
    }

    private static async Task<(Guid UnidadeId, Guid TipoId)> DestinoAsync(SmsMaisDbContext db)
    {
        var agora = DateTime.UtcNow;
        var unidade = new UnidadeAtendimento
        {
            Id = Guid.NewGuid(), Nome = $"CLÍNICA SIMULADA {Guid.NewGuid():N}"[..30].ToUpperInvariant(),
            Endereco = new Endereco
            {
                Cep = "24900000", Logradouro = "Rua Simulada", Numero = "100", Bairro = "Centro",
                Cidade = "Niterói", Uf = "RJ",
            },
            Gps = new Gps(-22.89, -43.12), Observacoes = "Entrada pela rampa lateral", Externa = true,
            Ativo = true, CriadoEm = agora,
        };
        var tipo = new TipoTratamento
        {
            Id = Guid.NewGuid(), Nome = "Hemodiálise", Codigo = $"hemo_{Guid.NewGuid():N}",
            TempoMedioMinutos = 240, Ativo = true, CriadoEm = agora,
        };
        db.UnidadesAtendimento.Add(unidade);
        db.TiposTratamento.Add(tipo);
        await db.SaveChangesAsync();
        return (unidade.Id, tipo.Id);
    }

    private static async Task<Guid> SessaoAsync(
        SmsMaisDbContext db, Guid pacienteId, Guid unidadeId, Guid tipoId,
        MobilidadeTransporte mobilidade = MobilidadeTransporte.Independente, bool? acompanhanteEsperado = null,
        StatusSessao status = StatusSessao.Pendente, DateOnly? dia = null)
    {
        var agora = DateTime.UtcNow;
        var data = dia ?? FusoBrasilia.HojeEmBrasilia();
        var tratamento = new Tratamento
        {
            Id = Guid.NewGuid(), PacienteId = pacienteId, UnidadeAtendimentoId = unidadeId, TipoTratamentoId = tipoId,
            Descricao = "Atendimento simulado", DataInicio = data, DiasSemanaMascara = 127,
            Mobilidade = mobilidade, NecessitaAjuda = mobilidade != MobilidadeTransporte.Independente,
            AjudaDescricao = mobilidade != MobilidadeTransporte.Independente ? "Precisa de ajuda para subir" : null,
            Ativo = true, CriadoEm = agora,
        };
        var sessao = new SessaoDeTratamento
        {
            Id = Guid.NewGuid(), TratamentoId = tratamento.Id, DataPrevista = data,
            Status = status, AcompanhanteEsperado = acompanhanteEsperado, CriadoEm = agora,
        };
        db.Tratamentos.Add(tratamento);
        db.Sessoes.Add(sessao);
        await db.SaveChangesAsync();
        return sessao.Id;
    }

    private static async Task<Guid> RotaAsync(
        SmsMaisDbContext db, Frota f, StatusRota status = StatusRota.Planejada, string? plano = null, DateOnly? data = null)
    {
        var rota = new RotaDiaria
        {
            Id = Guid.NewGuid(), Data = data ?? FusoBrasilia.HojeEmBrasilia(), VeiculoId = f.Tablet.VeiculoId,
            MotoristaId = f.MotoristaId, Status = status, PlanoRotaJson = plano, CriadoEm = DateTime.UtcNow,
        };
        db.Rotas.Add(rota);
        await db.SaveChangesAsync();
        return rota.Id;
    }

    private static async Task AlocarAsync(
        SmsMaisDbContext db, Guid rotaId, Guid sessaoId, Assento assento, TipoAlocacao tipo, int? ordem)
    {
        db.Alocacoes.Add(new Alocacao
        {
            Id = Guid.NewGuid(), RotaDiariaId = rotaId, SessaoId = sessaoId, AssentoId = assento.Id, Tipo = tipo,
            OrdemParada = ordem, Parada = TipoParada.Coleta, CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static PacienteDto PacienteFicticio(Guid id, string nome, EnderecoDto? endereco) =>
        new PacienteDto(
            id, nome, "00000000000", null, 0, 0, true, DateTime.UtcNow,
            null, new DateOnly(1970, 1, 1), default, default, default, default, null, null, "Brasileira",
            null, null, null, null,
            null, null, null, null, null,
            null, null, default, default, [], [], [], [], null,
            null, null) with { Endereco = endereco };

    [Fact]
    public async Task Entrega_coletas_na_ordem_destino_com_a_unidade_e_os_passageiros()
    {
        await using var db = fixture.CriarDbContext();
        var f = await VeiculoComTabletAsync(db);
        var (unidadeId, tipoId) = await DestinoAsync(db);
        var (anaId, beatrizId) = (Guid.NewGuid(), Guid.NewGuid());
        var sessaoAna = await SessaoAsync(db, anaId, unidadeId, tipoId, acompanhanteEsperado: true);
        var sessaoBeatriz = await SessaoAsync(db, beatrizId, unidadeId, tipoId,
            MobilidadeTransporte.CadeiranteVeiculoAdaptado, status: StatusSessao.Confirmada);

        // Ana tem o ponto no plano gravado pelo gerador; Beatriz vem pelo endereço (geocodificador).
        var plano = $$"""{"paradas":[{"ordem":2,"sessaoId":"{{sessaoAna}}","lat":-22.91,"lng":-42.82}]}""";
        var rotaId = await RotaAsync(db, f, plano: plano);
        await AlocarAsync(db, rotaId, sessaoAna, f.Assentos[1], TipoAlocacao.Paciente, 2);
        await AlocarAsync(db, rotaId, sessaoAna, f.Assentos[3], TipoAlocacao.Acompanhante, 2);
        await AlocarAsync(db, rotaId, sessaoBeatriz, f.Assentos[0], TipoAlocacao.Paciente, 1);

        var acompanhante = new Acompanhante
        {
            Id = Guid.NewGuid(), PacienteId = anaId, Cpf = "00000000191", Nome = "Carlos Simulado",
            DataNascimento = new DateOnly(1980, 1, 1), Parentesco = ParentescoAcompanhante.Filho,
            Telefone = "21900000000", CriadoEm = DateTime.UtcNow,
        };
        db.Acompanhantes.Add(acompanhante);
        db.SessoesAcompanhantes.Add(new SessaoAcompanhante { SessaoId = sessaoAna, AcompanhanteId = acompanhante.Id });
        await db.SaveChangesAsync();

        _resolver.ResolverManyAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, PacienteResumo>
            {
                [anaId] = new(anaId, "Ana Simulada", null, null, null, Sexo.Feminino, Celular: "21911111111"),
                [beatrizId] = new(beatrizId, "Beatriz Simulada", null, null, null, Sexo.Feminino),
            });
        var endereco = new EnderecoDto("24900000", "Rua das Acácias", "7", null, "Itaipuaçu", "Maricá", "RJ", "Portão azul");
        _pacientes.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(c => PacienteFicticio((Guid)c[0], "x", endereco));
        _geo.GeocodificarAsync(Arg.Any<Endereco?>(), Arg.Any<CancellationToken>())
            .Returns(new Coordenada(-22.95, -42.95));

        var rota = Assert.Single(await Servico(db).RotasDoDiaAsync(f.Tablet));

        Assert.Equal(rotaId, rota.RotaId);
        Assert.Equal("Motorista Simulado", rota.MotoristaNome);
        Assert.Collection(rota.Paradas,
            beatriz =>
            {
                Assert.Equal(TipoParada.Coleta, beatriz.Tipo);
                Assert.Equal("Beatriz Simulada", beatriz.Rotulo);
                Assert.Equal(-22.95, beatriz.Latitude); // do endereço
                Assert.Equal("Rua das Acácias, 7 — Itaipuaçu, Maricá", beatriz.Endereco);
                Assert.Equal("Portão azul", beatriz.Observacoes);
                var p = Assert.Single(beatriz.Passageiros);
                Assert.Equal(RotaTabletService.Confirmado, p.Confirmacao);
                Assert.Equal(MobilidadeTransporte.CadeiranteVeiculoAdaptado, p.Condicao.Mobilidade);
                Assert.Equal(1, p.Assento!.Numero);
                Assert.Empty(p.Acompanhantes);
            },
            ana =>
            {
                Assert.Equal("Ana Simulada", ana.Rotulo);
                Assert.Equal(-22.91, ana.Latitude); // do plano
                var p = Assert.Single(ana.Passageiros);
                Assert.Equal("21911111111", p.Telefone);
                Assert.Equal(RotaTabletService.Aguardando, p.Confirmacao);
                Assert.Equal("Hemodiálise", p.TipoTratamento);
                Assert.Equal(240, p.TempoMedioMinutos);
                var acomp = Assert.Single(p.Acompanhantes);
                Assert.Equal("Carlos Simulado", acomp.Nome);
                Assert.Equal(ParentescoAcompanhante.Filho, acomp.Parentesco);
                Assert.Equal(RotaTabletService.Confirmado, acomp.Confirmacao);
                Assert.Equal(TipoAssento.Acompanhante, acomp.Assento!.Tipo);
            },
            destino =>
            {
                Assert.Equal(TipoParada.Destino, destino.Tipo);
                Assert.Equal(3, destino.Ordem);
                Assert.Equal(-22.89, destino.Latitude);
                Assert.Equal("Entrada pela rampa lateral", destino.Observacoes);
                Assert.Equal("Rua Simulada, 100 — Centro, Niterói", destino.Endereco);
                Assert.Equal(2, destino.Passageiros.Count);
            });
    }

    [Fact]
    public async Task So_as_rotas_de_hoje_do_proprio_veiculo_e_sem_as_canceladas()
    {
        await using var db = fixture.CriarDbContext();
        var f = await VeiculoComTabletAsync(db);
        var outro = await VeiculoComTabletAsync(db);
        var hoje = FusoBrasilia.HojeEmBrasilia();

        var deHoje = await RotaAsync(db, f);
        await RotaAsync(db, f, StatusRota.Cancelada);
        await RotaAsync(db, f, data: hoje.AddDays(1));
        var voltaDeOntem = await RotaAsync(db, f, StatusRota.EmAndamento, data: hoje.AddDays(-1));
        await RotaAsync(db, outro);
        _resolver.ResolverManyAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, PacienteResumo>());

        var rotas = await Servico(db).RotasDoDiaAsync(f.Tablet);

        Assert.Equal([voltaDeOntem, deHoje], rotas.Select(r => r.RotaId));
        Assert.All(rotas, r => Assert.Empty(r.Paradas));
    }

    [Fact]
    public async Task Hub_fora_do_ar_nao_derruba_a_rota()
    {
        await using var db = fixture.CriarDbContext();
        var f = await VeiculoComTabletAsync(db);
        var (unidadeId, tipoId) = await DestinoAsync(db);
        var sessao = await SessaoAsync(db, Guid.NewGuid(), unidadeId, tipoId);
        var rotaId = await RotaAsync(db, f);
        await AlocarAsync(db, rotaId, sessao, f.Assentos[0], TipoAlocacao.Paciente, null);
        _resolver.ResolverManyAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyDictionary<Guid, PacienteResumo>>(_ => throw new HttpRequestException("hub fora"));
        _pacientes.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<PacienteDto>(_ => throw new HttpRequestException("hub fora"));

        var rota = Assert.Single(await Servico(db).RotasDoDiaAsync(f.Tablet));

        var coleta = rota.Paradas[0];
        Assert.Equal("Paciente", coleta.Rotulo);
        Assert.Null(coleta.Latitude);
        Assert.Equal(TipoParada.Destino, rota.Paradas[1].Tipo);
    }

    [Fact]
    public async Task Gerar_e_salvar_de_novo_nao_apaga_rota_em_andamento_nem_historico()
    {
        await using var db = fixture.CriarDbContext();
        var f = await VeiculoComTabletAsync(db);
        // Dia sem nenhuma sessão de outros testes: a geração só passa pela troca das rotas antigas.
        var dia = new DateOnly(2001, 1, 2);
        var emAndamento = await RotaGeradaAsync(db, f, dia, StatusRota.EmAndamento);
        var concluida = await RotaGeradaAsync(db, f, dia, StatusRota.Concluida);
        var cancelada = await RotaGeradaAsync(db, f, dia, StatusRota.Cancelada);
        var planejada = await RotaGeradaAsync(db, f, dia, StatusRota.Planejada);

        await Gerador(db).GerarAsync(new GerarTransladoRequest(dia, Confirmar: true, UsarIa: false));

        var restantes = await db.Rotas.AsNoTracking().Where(r => r.Data == dia && r.VeiculoId == f.Tablet.VeiculoId)
            .Select(r => r.Id).ToListAsync();
        Assert.Contains(emAndamento, restantes);
        Assert.Contains(concluida, restantes);
        Assert.Contains(cancelada, restantes);
        Assert.DoesNotContain(planejada, restantes);
    }

    [Fact]
    public async Task Gerar_e_salvar_de_novo_replaneja_quem_estava_na_rota_planejada()
    {
        await using var db = fixture.CriarDbContext();
        var f = await VeiculoComTabletAsync(db);
        var (unidadeId, tipoId) = await DestinoAsync(db);
        // Dia antigo e só deste teste: nenhuma sessão de outro teste entra na geração.
        var dia = new DateOnly(1999, 1, 5);
        var anaId = Guid.NewGuid();
        var sessao = await SessaoAsync(db, anaId, unidadeId, tipoId, dia: dia);
        var planejada = await RotaGeradaAsync(db, f, dia, StatusRota.Planejada);
        await AlocarAsync(db, planejada, sessao, f.Assentos[0], TipoAlocacao.Paciente, 1);

        var endereco = new EnderecoDto("24900000", "Rua das Acácias", "7", null, "Itaipuaçu", "Maricá", "RJ", null);
        _pacientes.ObterPorIdAsync(anaId, Arg.Any<CancellationToken>()).Returns(PacienteFicticio(anaId, "Ana Simulada", endereco));
        _geo.GeocodificarAsync(Arg.Any<Endereco?>(), Arg.Any<CancellationToken>()).Returns(new Coordenada(-22.95, -42.95));

        var resultado = await Gerador(db).GerarAsync(new GerarTransladoRequest(dia, Confirmar: true, UsarIa: false));

        Assert.Contains(resultado.Rotas, r => r.Paradas.Any(p => p.SessaoId == sessao));
        var alocacao = await db.Alocacoes.AsNoTracking()
            .SingleAsync(a => a.SessaoId == sessao && a.Tipo == TipoAlocacao.Paciente);
        Assert.NotEqual(planejada, alocacao.RotaDiariaId);
        Assert.False(await db.Rotas.AnyAsync(r => r.Id == planejada));
    }

    private GeradorDeTransladoService Gerador(SmsMaisDbContext db) =>
        new(db, _geo, Substitute.For<IGoogleRoutesClient>(), Substitute.For<IDistribuidorIa>(), _pacientes,
            NullLogger<GeradorDeTransladoService>.Instance);

    private static async Task<Guid> RotaGeradaAsync(SmsMaisDbContext db, Frota f, DateOnly dia, StatusRota status)
    {
        var id = await RotaAsync(db, f, status, data: dia);
        var rota = await db.Rotas.SingleAsync(r => r.Id == id);
        rota.Origem = OrigemRota.Gerada;
        await db.SaveChangesAsync();
        return id;
    }
}
