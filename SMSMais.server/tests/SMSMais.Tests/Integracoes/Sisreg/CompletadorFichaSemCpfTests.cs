using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Cadastro;
using SMSMais.Core.Integracoes.SisregWeb;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Ficha achada por CNS mas SEM CPF não pode ser ponto final da importação.
///
/// <para><b>O caso que motivou (Marcia, 28/09/2026):</b> o SER plantou a metade "só CNS" e o
/// Klinikos a metade "só CPF" da mesma mulher. A marcação do SISREG resolvia por CNS, caía na
/// metade sem CPF, e o desafio de verificação por WhatsApp perguntava os 4 primeiros dígitos de
/// um CPF que a ficha comparada não tinha — a paciente respondeu o próprio CPF, correto, e
/// queimou as chances contra um campo vazio. O completador fecha isso na origem: consulta o
/// CADSUS, e ou carimba o CPF na ficha, ou descobre o dono do CPF e o usa.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class CompletadorFichaSemCpfTests(PostgresFixture fixture)
{
    // CPF com dígito verificador válido (gerado para teste) e CNS sintático.
    private const string CpfValido = "52998224725";

    // A bancada é compartilhada e os testes deixam linhas para trás (aviso da PostgresFixture):
    // cada teste usa um CNS próprio para a memória de um não contaminar o outro.
    private readonly string Cns = NovoCns();

    private static string NovoCns() =>
        "7" + Random.Shared.NextInt64(10_000_000_000_000, 99_999_999_999_999).ToString();

    private static PacienteExistenciaDto Ficha(Guid? id = null, string cpf = "") =>
        new(id ?? Guid.CreateVersion7(), "FULANA DE TESTE", cpf, Ativo: true);

    private ConsultaCnsRespostaDto Cadsus(string cpf) => new(
        Cns: Cns, Cpf: cpf, Nome: "FULANA DE TESTE",
        Sexo: "Feminino", DataNascimento: new DateOnly(1970, 1, 22),
        NomeMae: "MARIA GENITORA DE TESTE", NomePai: "JOSE GENITOR DE TESTE");

    private sealed record Cenario(
        CompletadorFichaSemCpf Completador,
        IPacientesService Pacientes,
        ICadastroPacienteService Cadastro,
        SmsMaisDbContext Db);

    private Cenario Criar(SmsMaisDbContext db)
    {
        var pacientes = Substitute.For<IPacientesService>();
        var cadastro = Substitute.For<ICadastroPacienteService>();
        var completador = new CompletadorFichaSemCpf(
            db, cadastro, pacientes,
            Substitute.For<SMSMais.Core.Auditoria.IAuditoriaService>(),
            NullLogger<CompletadorFichaSemCpf>.Instance);
        return new Cenario(completador, pacientes, cadastro, db);
    }

    [Fact]
    public async Task Ficha_que_ja_tem_cpf_nao_consulta_nada()
    {
        await using var db = fixture.CriarDbContext();
        var c = Criar(db);

        var r = await c.Completador.CompletarAsync(Ficha(cpf: CpfValido), Cns);

        Assert.Null(r.Passo);
        await c.Cadastro.DidNotReceiveWithAnyArgs().ConsultarPorCnsAsync(default!, default);
    }

    [Fact]
    public async Task Cpf_livre_e_carimbado_na_propria_ficha_com_nascimento()
    {
        await using var db = fixture.CriarDbContext();
        var c = Criar(db);
        var ficha = Ficha();
        c.Cadastro.ConsultarPorCnsAsync(Cns, Arg.Any<CancellationToken>()).Returns(Cadsus(CpfValido));
        c.Pacientes.ObterPorCpfAsync(CpfValido, Arg.Any<CancellationToken>())
            .Returns((PacienteExistenciaDto?)null);

        var r = await c.Completador.CompletarAsync(ficha, Cns);

        Assert.Equal(ficha.Id, r.PacienteId);
        Assert.Contains("completada pelo CADSUS", r.Passo);
        await c.Pacientes.Received(1).DefinirCpfAsync(ficha.Id, CpfValido, Arg.Any<CancellationToken>());
        await c.Pacientes.Received(1).CompletarNascimentoAsync(
            ficha.Id, new DateOnly(1970, 1, 22), Arg.Any<CancellationToken>());
        await c.Pacientes.Received(1).CompletarFiliacaoAsync(
            ficha.Id, "MARIA GENITORA DE TESTE", "JOSE GENITOR DE TESTE", Arg.Any<CancellationToken>());

        await db.SaveChangesAsync(); // a memória viaja na transação do importador
        var memoria = await db.CadsusCompletudes.FindAsync(Cns);
        Assert.Equal(DesfechoCadsusCompletude.CpfCarimbado, memoria!.Desfecho);
    }

    [Fact]
    public async Task Cpf_de_outro_cadastro_reponta_para_o_dono_e_absorve_o_cns()
    {
        await using var db = fixture.CriarDbContext();
        var c = Criar(db);
        var sombra = Ficha();
        var dono = Ficha(cpf: CpfValido);
        c.Cadastro.ConsultarPorCnsAsync(Cns, Arg.Any<CancellationToken>()).Returns(Cadsus(CpfValido));
        c.Pacientes.ObterPorCpfAsync(CpfValido, Arg.Any<CancellationToken>()).Returns(dono);

        var r = await c.Completador.CompletarAsync(sombra, Cns);

        Assert.Equal(dono.Id, r.PacienteId);
        Assert.Contains("usa esse cadastro", r.Passo);
        await c.Pacientes.Received(1).AbsorverIdentificadoresAsync(
            dono.Id, Cns, null, Arg.Any<CancellationToken>());
        await c.Pacientes.DidNotReceiveWithAnyArgs().DefinirCpfAsync(default, default!, default);

        await db.SaveChangesAsync();
        var memoria = await db.CadsusCompletudes.FindAsync(Cns);
        Assert.Equal(DesfechoCadsusCompletude.RepontadoParaExistente, memoria!.Desfecho);
        Assert.Equal(dono.Id, memoria.PacienteDestinoId);
    }

    [Fact]
    public async Task Memoria_de_repontado_redireciona_sem_nova_consulta()
    {
        await using var db = fixture.CriarDbContext();
        var c = Criar(db);
        var dono = Ficha(cpf: CpfValido);
        db.CadsusCompletudes.Add(new SMSMais.Data.Entities.Sisreg.CadsusCompletude
        {
            Cns = Cns,
            ConsultadoEm = DateTime.UtcNow,
            Desfecho = DesfechoCadsusCompletude.RepontadoParaExistente,
            PacienteDestinoId = dono.Id,
        });
        await db.SaveChangesAsync();
        c.Pacientes.ObterPorIdAsync(dono.Id, Arg.Any<CancellationToken>())
            .Returns(PacienteDtoFabrica.Criar(dono.Id, dono.NomeCompleto, CpfValido));

        var r = await c.Completador.CompletarAsync(Ficha(), Cns);

        Assert.Equal(dono.Id, r.PacienteId);
        await c.Cadastro.DidNotReceiveWithAnyArgs().ConsultarPorCnsAsync(default!, default);

        var destino = await c.Completador.DestinoMemorizadoAsync(Cns);
        Assert.Equal(dono.Id, destino!.Id);
    }

    [Fact]
    public async Task Sem_cpf_valido_no_cadsus_ainda_completa_o_nascimento()
    {
        // 281 de 425 fichas caíram em "SemCpf" no primeiro backfill (29/09/2026) — mas a ficha
        // do CADSUS traz o nascimento, e ele sozinho torna o desafio do WhatsApp respondível.
        await using var db = fixture.CriarDbContext();
        var c = Criar(db);
        var ficha = Ficha();
        c.Cadastro.ConsultarPorCnsAsync(Cns, Arg.Any<CancellationToken>())
            .Returns(Cadsus(cpf: string.Empty));

        var r = await c.Completador.CompletarAsync(ficha, Cns);

        Assert.Equal(ficha.Id, r.PacienteId);
        Assert.Contains("nascimento foi completado", r.Passo);
        await c.Pacientes.Received(1).CompletarNascimentoAsync(
            ficha.Id, new DateOnly(1970, 1, 22), Arg.Any<CancellationToken>());
        await c.Pacientes.Received(1).CompletarFiliacaoAsync(
            ficha.Id, "MARIA GENITORA DE TESTE", "JOSE GENITOR DE TESTE", Arg.Any<CancellationToken>());
        await db.SaveChangesAsync();
        Assert.Equal(DesfechoCadsusCompletude.SemCpf, (await db.CadsusCompletudes.FindAsync(Cns))!.Desfecho);
    }

    [Fact]
    public async Task Cns_fora_do_cadsus_memoriza_e_nao_repergunta_no_mesmo_dia()
    {
        await using var db = fixture.CriarDbContext();
        var c = Criar(db);
        c.Cadastro.ConsultarPorCnsAsync(Cns, Arg.Any<CancellationToken>())
            .ThrowsAsync(new NaoEncontradoException("sisreg.paciente_nao_encontrado", "não achou"));

        var primeira = await c.Completador.CompletarAsync(Ficha(), Cns);
        Assert.Contains("não está no CADSUS", primeira.Passo);
        await db.SaveChangesAsync();

        var segunda = await c.Completador.CompletarAsync(Ficha(), Cns);
        Assert.Null(segunda.Passo);
        await c.Cadastro.Received(1).ConsultarPorCnsAsync(Cns, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fonte_indisponivel_nao_memoriza_nem_derruba()
    {
        await using var db = fixture.CriarDbContext();
        var c = Criar(db);
        var ficha = Ficha();
        c.Cadastro.ConsultarPorCnsAsync(Cns, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("timeout"));

        var r = await c.Completador.CompletarAsync(ficha, Cns);

        Assert.Equal(ficha.Id, r.PacienteId);
        Assert.Null(r.Passo);
        await db.SaveChangesAsync();
        Assert.Null(await db.CadsusCompletudes.FindAsync(Cns));
    }

    [Fact]
    public async Task Teto_por_escopo_para_de_consultar_mas_nao_derruba()
    {
        await using var db = fixture.CriarDbContext();
        var c = Criar(db);
        c.Cadastro.ConsultarPorCnsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("timeout")); // indisponível: não memoriza, conta consulta

        for (var i = 0; i < 25; i++)
            await c.Completador.CompletarAsync(Ficha(), NovoCns());

        var alem = await c.Completador.CompletarAsync(Ficha(), Cns);
        Assert.Null(alem.Passo);
        await c.Cadastro.Received(25).ConsultarPorCnsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
