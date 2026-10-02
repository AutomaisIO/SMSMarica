using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Regulacao.Comum;
using SMSMais.Core.Regulacao.Medicos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Data.Entities.Ser;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.Medicos;

/// <summary>
/// A régua do "este médico já existe?" — casos reais do combo do SER (01/10/2026). O que prendem:
/// o cadastro abreviado é achado pelo nome completo, e só iniciais não bastam.
/// </summary>
public class SemelhancaNomeTests
{
    [Theory]
    [InlineData("LAURA BEATRIZ ANDRADE RODRIGUES", "LAURA BEATRIZ A. RODRIGUES VILELA")]
    [InlineData("Laura Beatriz Andrade Rodrigues", "LAURA BEATRIZ A. RODRIGUES VILELA")]
    [InlineData("ADRIANA CHALHUB", "ADRIANA CHALHUB")]
    [InlineData("JOSÉ DA SILVA PEREIRA", "JOSE SILVA PEREIRA")]
    [InlineData("MARCELO RODRIGUS ALVES", "MARCELO RODRIGUES ALVES")]
    public void Acha_o_mesmo_medico(string digitado, string cadastro) =>
        SemelhancaNome.Pontuacao(digitado, cadastro).Should().BeGreaterThanOrEqualTo(0.75);

    [Theory]
    [InlineData("LAURA BEATRIZ ANDRADE RODRIGUES", "MAXIMILIANO L. ROSA")]
    [InlineData("ADRIANA CHALHUB", "JULIO A C GAMA")]
    [InlineData("LAURA BEATRIZ", "BEATRIZ LAURA SOUZA")]
    public void Nao_acha_quem_nao_e(string digitado, string cadastro) =>
        SemelhancaNome.Pontuacao(digitado, cadastro).Should().Be(0);

    [Fact]
    public void Crm_compara_so_os_digitos() =>
        SemelhancaNome.Digitos("52.140159-2").Should().Be("521401592");
}

/// <summary>
/// O médico pedido na abertura fica PENDENTE e só a regulação o resolve. O que prendem: o pedido
/// repetido não vira dois; a resolução troca o médico em todas as solicitações que o usavam; e
/// recusar exige motivo, que é o que a unidade lê.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RegulacaoMedicoPendenteTests(PostgresFixture fixture)
{
    private static string Sufixo() =>
        new([.. Guid.NewGuid().ToString("N").Where(char.IsLetter).Take(6).Select(char.ToUpperInvariant)]);

    private static RegulacaoMedicoPendenteService Servico(SmsMaisDbContext db) =>
        new(db, Substitute.For<IRegulacaoEscopo>(), new UsuarioAtualAccessorFake(Guid.NewGuid()));

    [Fact]
    public async Task Pedido_grava_em_maiusculas_e_o_mesmo_nome_nao_vira_dois()
    {
        await using var db = fixture.CriarDbContext();
        var nome = $"helena  teste {Sufixo()} souza";

        var primeiro = await Servico(db).CriarAsync(
            new CriarMedicoPendenteRequest(SistemaRegulacao.Ser, nome, "crm", "52140159-2", "genética"), CancellationToken.None);
        var segundo = await Servico(db).CriarAsync(
            new CriarMedicoPendenteRequest(SistemaRegulacao.Ser, nome.ToUpperInvariant(), null, null, null), CancellationToken.None);

        primeiro.Nome.Should().Be(string.Join(' ', nome.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant());
        primeiro.TipoDocumento.Should().Be("CRM");
        primeiro.Valor.Should().Be($"pendente:{primeiro.Id}");
        segundo.Id.Should().Be(primeiro.Id, "outra unidade pedindo o mesmo médico reaproveita o pedido");
    }

    [Fact]
    public async Task Sistema_sem_lista_de_medicos_nao_aceita_pedido()
    {
        await using var db = fixture.CriarDbContext();
        var acao = () => Servico(db).CriarAsync(
            new CriarMedicoPendenteRequest(SistemaRegulacao.Sisreg, "FULANO DE TAL", null, null, null), CancellationToken.None);
        await acao.Should().ThrowAsync<ValidacaoException>();
    }

    [Fact]
    public async Task Parecidos_acha_o_cadastro_abreviado_da_lista_do_SER()
    {
        await using var db = fixture.CriarDbContext();
        var sobrenome = Sufixo();
        var abreviado = $"QUITERIA BEATRIZ A. {sobrenome} VILELA";
        db.SerCatalogoListas.Add(new SerCatalogoLista
        {
            Id = Guid.NewGuid(), Lista = "medico", Valor = Guid.NewGuid().ToString("N")[..12],
            Rotulo = abreviado, Ordem = 0, SincronizadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var parecidos = await Servico(db).ParecidosAsync(
            SistemaRegulacao.Ser, $"Quitéria Beatriz Andrade {sobrenome}", null, CancellationToken.None);

        parecidos.Should().Contain(p => p.Nome == abreviado && p.Origem == "Sistema");
    }

    [Fact]
    public async Task Ja_existia_troca_o_medico_nas_solicitacoes_que_usavam_o_pendente()
    {
        await using var db = fixture.CriarDbContext();
        var servico = Servico(db);
        var pendente = await servico.CriarAsync(
            new CriarMedicoPendenteRequest(SistemaRegulacao.Ser, $"RAIMUNDA TESTE {Sufixo()} LIMA", "CRM", "123", null),
            CancellationToken.None);
        var solicitacaoId = await SolicitacaoComMedicoAsync(db, pendente.Valor);

        db.ChangeTracker.Clear();
        var resolvido = await Servico(db).ResolverAsync(
            pendente.Id,
            new ResolverMedicoPendenteRequest(SituacaoMedicoPendente.JaExistia, "RAIMUNDA T. LIMA", null),
            CancellationToken.None);

        resolvido.Situacao.Should().Be(SituacaoMedicoPendente.JaExistia);
        var formulario = (await db.RegulacaoSolicitacoes.AsNoTracking().FirstAsync(s => s.Id == solicitacaoId)).FormularioJson;
        JsonDocument.Parse(formulario).RootElement.GetProperty("canonico").GetProperty("medico_solicitante").GetString()
            .Should().Be("RAIMUNDA T. LIMA", "o envio acha o médico no combo pelo nome que está no sistema");
    }

    [Fact]
    public async Task Recusar_exige_motivo_e_nao_resolve_duas_vezes()
    {
        await using var db = fixture.CriarDbContext();
        var pendente = await Servico(db).CriarAsync(
            new CriarMedicoPendenteRequest(SistemaRegulacao.Sernit, $"OTACILIO TESTE {Sufixo()} REIS", null, null, null),
            CancellationToken.None);

        var semMotivo = () => Servico(db).ResolverAsync(
            pendente.Id, new ResolverMedicoPendenteRequest(SituacaoMedicoPendente.Recusado, null, " "), CancellationToken.None);
        await semMotivo.Should().ThrowAsync<ValidacaoException>();

        db.ChangeTracker.Clear();
        await Servico(db).ResolverAsync(
            pendente.Id, new ResolverMedicoPendenteRequest(SituacaoMedicoPendente.Recusado, null, "CRM não confere"),
            CancellationToken.None);

        db.ChangeTracker.Clear();
        var deNovo = () => Servico(db).ResolverAsync(
            pendente.Id, new ResolverMedicoPendenteRequest(SituacaoMedicoPendente.Cadastrado, null, null), CancellationToken.None);
        await deNovo.Should().ThrowAsync<ConflitoException>();
    }

    private static async Task<Guid> SolicitacaoComMedicoAsync(SmsMaisDbContext db, string valorMedico)
    {
        var sufixo = Sufixo();
        var unidade = new Unidade { Id = Guid.NewGuid(), Nome = $"UNID MEDICO {sufixo}", CriadoEm = DateTime.UtcNow };
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(), NomeCompleto = "SOLICITANTE MEDICO", Email = $"{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x", Ativo = true, CriadoEm = DateTime.UtcNow,
        };
        var procedimento = new RegulacaoProcedimento
        {
            Id = Guid.CreateVersion7(), NomeCanonico = $"PROC MEDICO {sufixo}", NomeNormalizado = "PROC MEDICO",
            Tipo = TipoProcedimentoRegulacao.Consulta, CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);
        db.Usuarios.Add(usuario);
        db.RegulacaoProcedimentos.Add(procedimento);
        await db.SaveChangesAsync();

        var s = new RegulacaoSolicitacao
        {
            Id = Guid.CreateVersion7(),
            Fluxo = FluxoRegulacao.Externo,
            UnidadeSolicitanteId = unidade.Id,
            CriadoPorUsuarioId = usuario.Id,
            PacienteId = Guid.NewGuid(),
            PacienteNome = "PACIENTE MEDICO",
            ProcedimentoId = procedimento.Id,
            SistemaDestino = SistemaRegulacao.Ser,
            FormularioJson = JsonSerializer.Serialize(new { canonico = new { medico_solicitante = valorMedico } }),
            Status = StatusRegulacao.PendenteRegulacao,
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoSolicitacoes.Add(s);
        await db.SaveChangesAsync();
        return s.Id;
    }
}
