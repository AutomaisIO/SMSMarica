using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SMSMais.Core.Common.Dtos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Faturamento;
using SMSMais.Core.Geo;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Tratamentos;
using SMSMais.Core.Tratamentos.Dtos;
using SMSMais.Core.UnidadesAtendimento;
using SMSMais.Core.UnidadesAtendimento.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.UnidadesAtendimento;

/// <summary>
/// Unidade de atendimento é o DESTINO do transporte: a coordenada dela é o fim da rota da van.
/// Por isso nenhuma entra sem ponto no mapa, e nenhuma sai de cena com tratamento ativo indo para
/// lá. O tratamento passa a apontar para ela (e carrega o tempo médio que o paciente fica lá).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class UnidadesAtendimentoServiceTests(PostgresFixture fixture)
{
    // A bancada guarda as linhas entre execuções: nome único por teste evita o conflito de nome.
    private static string NomeUnico(string prefixo) => $"{prefixo} {Guid.NewGuid():N}";

    private static EnderecoDto Endereco(string cep = "24020-000") =>
        new(cep, "Rua da Conceição", "100", null, "Centro", "Niterói", "rj", null);

    private static UnidadesAtendimentoService Servico(SmsMaisDbContext db, IGeocodificadorService? geo = null) =>
        new(db, geo ?? Substitute.For<IGeocodificadorService>(), new UsuarioAtualAccessorFake(Guid.NewGuid()));

    private static TratamentosService ServicoTratamentos(SmsMaisDbContext db) =>
        new(db, Substitute.For<IPacienteResolver>(), Substitute.For<IFaturamentoService>());

    private static CadastrarTratamentoRequest NovoTratamento(Guid destinoId, int tempoMedio = 240) => new(
        Guid.NewGuid(), destinoId, null, "Hemodiálise", null, null, new TimeOnly(6, 0), tempoMedio,
        new CadastrarPeriodicidadeRequest(TipoPeriodicidade.Manual, null, null, new DateOnly(2026, 10, 5), 1),
        [new DateOnly(2026, 10, 5)]);

    [Fact]
    public async Task Pin_do_mapa_vale_sem_geocodificar_nome_vira_maiusculas_e_cep_so_digitos()
    {
        await using var db = fixture.CriarDbContext();
        var geo = Substitute.For<IGeocodificadorService>();

        var nome = NomeUnico("Clínica  da   Alameda");
        var id = await Servico(db, geo).CadastrarAsync(new SalvarUnidadeAtendimentoRequest(
            $"  {nome} ", Endereco(), "(21) 2222-0000", "Entrada pelo portão 2", -22.9, -43.1));

        var salva = await db.UnidadesAtendimento.AsNoTracking().SingleAsync(u => u.Id == id);
        Assert.Equal(System.Text.RegularExpressions.Regex.Replace(nome, @"\s+", " ").ToUpperInvariant(), salva.Nome);
        Assert.StartsWith("CLÍNICA DA ALAMEDA ", salva.Nome);
        Assert.Equal(new Gps(-22.9, -43.1), salva.Gps);
        Assert.Equal("24020000", salva.Endereco!.Cep);
        Assert.Equal("RJ", salva.Endereco.Uf);
        Assert.True(salva.Externa);
        Assert.True(salva.Ativo);
        await geo.DidNotReceiveWithAnyArgs().GeocodificarAsync(default, default);
    }

    [Fact]
    public async Task Sem_pin_usa_a_geocodificacao_do_endereco()
    {
        await using var db = fixture.CriarDbContext();
        var geo = Substitute.For<IGeocodificadorService>();
        geo.GeocodificarAsync(Arg.Any<Endereco?>(), Arg.Any<CancellationToken>()).Returns(new Coordenada(-22.8, -43.0));

        var id = await Servico(db, geo).CadastrarAsync(new SalvarUnidadeAtendimentoRequest(
            NomeUnico("Hospital"), Endereco(), null, null, null, null));

        var salva = await db.UnidadesAtendimento.AsNoTracking().SingleAsync(u => u.Id == id);
        Assert.Equal(new Gps(-22.8, -43.0), salva.Gps);
    }

    [Fact]
    public async Task Sem_pin_e_sem_geocodificacao_recusa_porque_nao_ha_rota_sem_destino()
    {
        await using var db = fixture.CriarDbContext();
        var nome = NomeUnico("Sem mapa");

        await Assert.ThrowsAsync<ValidacaoException>(() => Servico(db).CadastrarAsync(
            new SalvarUnidadeAtendimentoRequest(nome, Endereco(), null, null, null, null)));

        Assert.False(await db.UnidadesAtendimento.AnyAsync(u => u.Nome == nome));
    }

    [Fact]
    public async Task Nome_repetido_entre_ativas_e_conflito_mesmo_com_caixa_e_espacos_diferentes()
    {
        await using var db = fixture.CriarDbContext();
        var nome = NomeUnico("Clínica Repetida");
        await Servico(db).CadastrarAsync(new SalvarUnidadeAtendimentoRequest(nome, Endereco(), null, null, -22.9, -43.1));

        await Assert.ThrowsAsync<ConflitoException>(() => Servico(db).CadastrarAsync(
            new SalvarUnidadeAtendimentoRequest($"  {nome.ToLowerInvariant().Replace(" ", "   ")} ", Endereco(), null, null, -22.9, -43.1)));
    }

    [Fact]
    public async Task Tratamento_grava_destino_e_tempo_medio_e_aparece_na_lista_da_unidade()
    {
        await using var db = fixture.CriarDbContext();
        var destinoId = await Servico(db).CadastrarAsync(new SalvarUnidadeAtendimentoRequest(
            NomeUnico("Nefro"), Endereco(), null, null, -22.9, -43.1));

        var tratamentoId = await ServicoTratamentos(db).CadastrarAsync(NovoTratamento(destinoId, tempoMedio: 270));

        var dto = await ServicoTratamentos(db).ObterPorIdAsync(tratamentoId);
        Assert.Equal(destinoId, dto.UnidadeAtendimentoId);
        Assert.Equal(270, dto.TempoMedioMinutos);
        var daUnidade = await ServicoTratamentos(db).ListarPorUnidadeAtendimentoAsync(destinoId);
        Assert.Contains(daUnidade, t => t.Id == tratamentoId && t.TempoMedioMinutos == 270);

        // A contagem de tratamentos ativos é a mesma na lista e no detalhe (é ela que trava o desativar).
        var lista = await Servico(db).ListarAsync(incluirInativas: false);
        var item = Assert.Single(lista, u => u.Id == destinoId);
        Assert.Equal(1, item.TratamentosAtivos);
        Assert.True(item.TemCoordenada);
        Assert.Equal(1, (await Servico(db).ObterPorIdAsync(destinoId)).TratamentosAtivos);
    }

    [Fact]
    public async Task Desativar_com_tratamento_ativo_e_recusado_e_depois_de_encerrar_passa()
    {
        await using var db = fixture.CriarDbContext();
        var servico = Servico(db);
        var destinoId = await servico.CadastrarAsync(new SalvarUnidadeAtendimentoRequest(
            NomeUnico("Destino ocupado"), Endereco(), null, null, -22.9, -43.1));
        var tratamentoId = await ServicoTratamentos(db).CadastrarAsync(NovoTratamento(destinoId));

        await Assert.ThrowsAsync<ConflitoException>(() => servico.DesativarAsync(destinoId));

        await ServicoTratamentos(db).EncerrarAsync(tratamentoId);
        await servico.DesativarAsync(destinoId);

        var opcoes = await servico.ListarOpcoesAsync();
        Assert.DoesNotContain(opcoes, o => o.Id == destinoId);
    }

    [Fact]
    public async Task Destino_desativado_nao_e_aceito_em_tratamento_novo_nem_na_troca()
    {
        await using var db = fixture.CriarDbContext();
        var servico = Servico(db);
        var ativoId = await servico.CadastrarAsync(new SalvarUnidadeAtendimentoRequest(
            NomeUnico("Ativo"), Endereco(), null, null, -22.9, -43.1));
        var inativoId = await servico.CadastrarAsync(new SalvarUnidadeAtendimentoRequest(
            NomeUnico("Inativo"), Endereco(), null, null, -22.9, -43.1));
        await servico.DesativarAsync(inativoId);

        await Assert.ThrowsAsync<ConflitoException>(() => ServicoTratamentos(db).CadastrarAsync(NovoTratamento(inativoId)));

        var tratamentoId = await ServicoTratamentos(db).CadastrarAsync(NovoTratamento(ativoId));
        await Assert.ThrowsAsync<ConflitoException>(() => ServicoTratamentos(db).AtualizarAsync(tratamentoId,
            new AtualizarTratamentoRequest("Hemodiálise", inativoId, null, null, null, null, 240)));
    }

    [Fact]
    public async Task Editar_troca_destino_e_tempo_medio()
    {
        await using var db = fixture.CriarDbContext();
        var servico = Servico(db);
        var origemId = await servico.CadastrarAsync(new SalvarUnidadeAtendimentoRequest(
            NomeUnico("Antigo"), Endereco(), null, null, -22.9, -43.1));
        var novoId = await servico.CadastrarAsync(new SalvarUnidadeAtendimentoRequest(
            NomeUnico("Novo"), Endereco(), null, null, -22.8, -43.2));
        var tratamentoId = await ServicoTratamentos(db).CadastrarAsync(NovoTratamento(origemId, tempoMedio: 240));

        await ServicoTratamentos(db).AtualizarAsync(tratamentoId,
            new AtualizarTratamentoRequest("Hemodiálise", novoId, null, null, null, new TimeOnly(5, 30), 210));

        await using var leitura = fixture.CriarDbContext();
        var salvo = await leitura.Tratamentos.AsNoTracking().SingleAsync(t => t.Id == tratamentoId);
        Assert.Equal(novoId, salvo.UnidadeAtendimentoId);
        Assert.Equal(210, salvo.TempoMedioMinutos);
    }
}
