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

    [Fact]
    public async Task Reservar_o_cadastro_pelo_modal_so_acontece_uma_vez()
    {
        // A trava do "Autorizo cadastrar": duplo clique ou dois reguladores não escrevem duas vezes.
        await using var db = fixture.CriarDbContext();
        var pendente = await Servico(db).CriarAsync(
            new CriarMedicoPendenteRequest(SistemaRegulacao.Ser, $"ROSALVA TESTE {Sufixo()} MOURA", "CRM", "5201216066", "ONCOLOGISTA"),
            CancellationToken.None);

        var reservado = await Servico(db).ReservarCadastroAsync(pendente.Id, CancellationToken.None);
        var deNovo = () => Servico(db).ReservarCadastroAsync(pendente.Id, CancellationToken.None);

        reservado.Situacao.Should().Be(SituacaoMedicoPendente.CadastroIncerto,
            "se o servidor cair no meio do Gravar, o estado que fica é o que manda conferir no sistema");
        await deNovo.Should().ThrowAsync<ConflitoException>();
    }

    [Fact]
    public async Task Cadastro_confirmado_troca_o_medico_nas_solicitacoes()
    {
        await using var db = fixture.CriarDbContext();
        var nome = $"ROSALVA TESTE {Sufixo()} MOURA";
        var pendente = await Servico(db).CriarAsync(
            new CriarMedicoPendenteRequest(SistemaRegulacao.Ser, nome, null, null, null), CancellationToken.None);
        var solicitacaoId = await SolicitacaoComMedicoAsync(db, pendente.Valor);
        await Servico(db).ReservarCadastroAsync(pendente.Id, CancellationToken.None);

        db.ChangeTracker.Clear();
        var cadastrado = await Servico(db).ConfirmarCadastroAsync(pendente.Id, nome + " ", CancellationToken.None);

        cadastrado.Situacao.Should().Be(SituacaoMedicoPendente.Cadastrado);
        cadastrado.NomeNoSistema.Should().Be(nome);
        var formulario = (await db.RegulacaoSolicitacoes.AsNoTracking().FirstAsync(s => s.Id == solicitacaoId)).FormularioJson;
        JsonDocument.Parse(formulario).RootElement.GetProperty("canonico").GetProperty("medico_solicitante").GetString()
            .Should().Be(nome);
    }

    [Fact]
    public async Task Cadastro_incerto_nao_aceita_cadastrei_mas_aceita_ja_existia_e_nao_entrou()
    {
        await using var db = fixture.CriarDbContext();
        var pendente = await Servico(db).CriarAsync(
            new CriarMedicoPendenteRequest(SistemaRegulacao.Ser, $"ROSALVA TESTE {Sufixo()} MOURA", null, null, null),
            CancellationToken.None);
        await Servico(db).ReservarCadastroAsync(pendente.Id, CancellationToken.None);

        db.ChangeTracker.Clear();
        var cadastrei = () => Servico(db).ResolverAsync(
            pendente.Id, new ResolverMedicoPendenteRequest(SituacaoMedicoPendente.Cadastrado, null, null), CancellationToken.None);
        await cadastrei.Should().ThrowAsync<ValidacaoException>("depois de um Gravar incerto, só se afirma o que se conferiu");

        db.ChangeTracker.Clear();
        var naoEntrou = await Servico(db).ResolverAsync(
            pendente.Id, new ResolverMedicoPendenteRequest(SituacaoMedicoPendente.Pendente, null, null), CancellationToken.None);
        naoEntrou.Situacao.Should().Be(SituacaoMedicoPendente.Pendente);

        db.ChangeTracker.Clear();
        var pendenteDeNovo = () => Servico(db).ResolverAsync(
            pendente.Id, new ResolverMedicoPendenteRequest(SituacaoMedicoPendente.Pendente, null, null), CancellationToken.None);
        await pendenteDeNovo.Should().ThrowAsync<ValidacaoException>("\"não entrou\" só existe depois de uma tentativa");

        await Servico(db).ReservarCadastroAsync(pendente.Id, CancellationToken.None);
        db.ChangeTracker.Clear();
        var jaExistia = await Servico(db).ResolverAsync(
            pendente.Id, new ResolverMedicoPendenteRequest(SituacaoMedicoPendente.JaExistia, "ROSALVA T. MOURA", null),
            CancellationToken.None);
        jaExistia.Situacao.Should().Be(SituacaoMedicoPendente.JaExistia);
    }

    [Fact]
    public async Task Parecidos_ao_vivo_usam_a_lista_de_hoje_e_ignoram_pendentes()
    {
        await using var db = fixture.CriarDbContext();
        var sobrenome = Sufixo();
        await Servico(db).CriarAsync(
            new CriarMedicoPendenteRequest(SistemaRegulacao.Ser, $"RAFAELA ROCHA {sobrenome}", null, null, null),
            CancellationToken.None);

        var parecidos = await Servico(db).ParecidosAsync(
            SistemaRegulacao.Ser, $"RAFAELA ROCHA {sobrenome} BEDRAN", null, CancellationToken.None,
            [$"RAFAELA R. {sobrenome} BEDRAN ", "OUTRO MEDICO QUALQUER"]);

        parecidos.Should().ContainSingle()
            .Which.Nome.Should().Be($"RAFAELA R. {sobrenome} BEDRAN", "o regulador escolhe entre o que o combo tem AGORA");
    }

    /// <summary>
    /// PR-18/PR-23 (09/10/2026): "Cadastrei" com o nome da unidade, e o médico nunca entrou no SERNIT —
    /// o envio barrava sem saída. Na prévia, o pedido de cadastro é reaberto para a solicitação, com o
    /// documento e a especialidade do pedido antigo; o antigo fica como estava (é a história).
    /// </summary>
    [Fact]
    public async Task Nome_dado_como_cadastrado_que_nao_esta_na_lista_reabre_o_pedido_na_solicitacao()
    {
        await using var db = fixture.CriarDbContext();
        var nome = $"LAURA TESTE {Sufixo()} VILELA";
        var antigo = await Servico(db).CriarAsync(
            new CriarMedicoPendenteRequest(SistemaRegulacao.Sernit, nome, "CRM", "52140159-2", "CLINICO GERAL"),
            CancellationToken.None);
        var solicitacaoId = await SolicitacaoComMedicoAsync(db, antigo.Valor);
        db.ChangeTracker.Clear();
        await Servico(db).ResolverAsync(
            antigo.Id, new ResolverMedicoPendenteRequest(SituacaoMedicoPendente.Cadastrado, null, null), CancellationToken.None);

        db.ChangeTracker.Clear();
        var reaberto = await Servico(db).ReabrirForaDaListaAsync(
            solicitacaoId, SistemaRegulacao.Sernit, nome, CancellationToken.None);

        reaberto.Id.Should().NotBe(antigo.Id);
        reaberto.Situacao.Should().Be(SituacaoMedicoPendente.Pendente);
        reaberto.Nome.Should().Be(nome);
        reaberto.TipoDocumento.Should().Be("CRM");
        reaberto.NumeroDocumento.Should().Be("52140159-2");
        reaberto.Especialidade.Should().Be("CLINICO GERAL");
        var formulario = (await db.RegulacaoSolicitacoes.AsNoTracking().FirstAsync(s => s.Id == solicitacaoId)).FormularioJson;
        JsonDocument.Parse(formulario).RootElement.GetProperty("canonico").GetProperty("medico_solicitante").GetString()
            .Should().Be(reaberto.Valor);
        (await Servico(db).ObterAsync(antigo.Id, CancellationToken.None)).Situacao
            .Should().Be(SituacaoMedicoPendente.Cadastrado, "o registro antigo é a história, não se reescreve");

        // A outra solicitação com o mesmo nome reaproveita o mesmo pedido reaberto.
        var outra = await SolicitacaoComMedicoAsync(db, nome);
        db.ChangeTracker.Clear();
        var deNovo = await Servico(db).ReabrirForaDaListaAsync(outra, SistemaRegulacao.Sernit, nome, CancellationToken.None);
        deNovo.Id.Should().Be(reaberto.Id);
    }

    [Fact]
    public async Task Reabrir_nao_troca_o_medico_se_ele_mudou_na_solicitacao()
    {
        await using var db = fixture.CriarDbContext();
        var solicitacaoId = await SolicitacaoComMedicoAsync(db, $"OUTRO MEDICO {Sufixo()} SILVA");

        db.ChangeTracker.Clear();
        var acao = () => Servico(db).ReabrirForaDaListaAsync(
            solicitacaoId, SistemaRegulacao.Sernit, $"LAURA TESTE {Sufixo()} VILELA", CancellationToken.None);

        await acao.Should().ThrowAsync<ConflitoException>();
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
