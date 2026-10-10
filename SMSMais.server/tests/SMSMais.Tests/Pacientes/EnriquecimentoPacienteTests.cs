using System.Text.Json.Nodes;
using FluentAssertions;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SMSMais.Core.Auditoria;
using SMSMais.Core.Common.Dtos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Dtos;
using SMSMais.Core.Integracoes.EsusPec;
using SMSMais.Core.Integracoes.Proxy;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Core.Pacientes.Enriquecimento;
using SMSMais.Core.Pacientes.Enriquecimento.Dtos;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Ser;
using SMSMais.Core.Ser.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

// `Hl7.Fhir.Model.Task` (o recurso FHIR) colide com o Task do runtime neste arquivo.
using Task = System.Threading.Tasks.Task;

namespace SMSMais.Tests.Pacientes;

/// <summary>
/// "Enriquecer" a ficha pelo CADSUS (SER) e pelo e-SUS PEC: o que se oferece, o que bloqueia e a
/// ordem de gravação — conferência na Receita ANTES de qualquer escrita.
/// </summary>
public class EnriquecimentoPacienteTests
{
    private const string Cpf = "52998224725";
    private const string CpfOutro = "11144477735";
    private static readonly Guid PacienteId = Guid.Parse("0199c0de-0000-7000-8000-000000000001");
    private static readonly Guid Operador = Guid.Parse("0199c0de-0000-7000-8000-0000000000aa");

    // ---------------------------------------------------------------- leitura das fontes

    private static SerCampoPacienteDto Campo(string campo, string rotulo, string? valor, IReadOnlyList<SerOpcaoDto>? opcoes = null) =>
        new(campo, rotulo, valor, opcoes is null ? "text" : "select", false, true, opcoes);

    [Fact]
    public void Painel_do_SER_vira_ficha_pelo_id_e_pelo_rotulo_com_o_texto_da_opcao_do_select()
    {
        var ficha = FichaExterna.DoSer(
        [
            Campo("form0:nome", "Nome", "MARIA DA SILVA"),
            Campo("form0:cpf", "CPF", "529.982.247-25"),
            Campo("form0:cns", "CNS", "700 1234 5678 9012"),
            Campo("form0:dataNascimento", "Data de Nascimento", "05/03/1980"),
            Campo("form0:sexo", "Sexo", "F", [new("M", "MASCULINO"), new("F", "FEMININO")]),
            Campo("form0:nomeMae", "Nome da Mãe", "JOANA DA SILVA"),
            Campo("form0:logradouro", "Logradouro", "RUA DAS FLORES"),
            Campo("form0:numero", "Número", "10"),
            Campo("form0:cep", "CEP", "24900000"),
            Campo("form0:uf", "UF", "58", [new("58", "RIO DE JANEIRO")]),
            Campo("form0:municipio", "Município", "3302", [new("3302", "MARICÁ")]),
            Campo("form0:bairro", "Bairro", "CENTRO"),
            Campo("form0:raca", "Raça", "03", [new("03", "PARDA")]),
            // Telefones: dois com id POSICIONAL — é o rótulo que diz o tipo.
            Campo("form0:j_id173", "Telefone Residencial", "(21) 2637-1234"),
            Campo("form0:j_id178", "Telefone WhatsApp", "(21) 99876-5432"),
            Campo("form0:telefoneContato", "Telefone Contato", "21 98888-7777"),
        ], ["O CNS definitivo é diferente do CNS provisório"]);

        ficha.Cpf.Should().Be(Cpf);
        ficha.Cns.Should().Be("700123456789012");
        ficha.DataNascimento.Should().Be(new DateOnly(1980, 3, 5));
        ficha.Sexo.Should().Be(Sexo.Feminino);
        ficha.RacaCor.Should().Be(RacaCor.Parda);
        ficha.Endereco!.Uf.Should().Be("RJ", "o select manda código; a ficha guarda a sigla");
        ficha.Endereco.Cidade.Should().Be("MARICÁ");
        ficha.Endereco.Cep.Should().Be("24900-000");
        ficha.Telefones.Select(t => (t.Numero, t.Tipo)).Should().BeEquivalentTo(new[]
        {
            ("2126371234", "residencial"), ("21998765432", "celular"), ("21988887777", "celular"),
        });
        ficha.Avisos.Should().ContainSingle();
    }

    [Fact]
    public void Ficha_do_esus_junta_tipo_e_nome_do_logradouro_e_avisa_obito()
    {
        var json = JsonNode.Parse("""
            {
              "id": "77", "cpf": "52998224725", "cns": "898001234567890", "nome": "MARIA DA SILVA",
              "nomeSocial": null, "dataNascimento": "1980-03-05", "dataAtualizado": 1759276800000,
              "sexo": "FEMININO", "nomeMae": "JOANA DA SILVA", "nomePai": "JOSE DA SILVA", "email": "Maria@Exemplo.com",
              "telefoneResidencial": null, "telefoneCelular": "21998765432", "telefoneContato": "99999999999",
              "faleceu": true, "ativo": true,
              "endereco": { "cep": "24900000", "uf": { "nome": "RIO DE JANEIRO" }, "municipio": { "nome": "MARICÁ" },
                "bairro": "CENTRO", "tipoLogradouro": { "nome": "AVENIDA" }, "logradouro": "ROBERTO SILVEIRA",
                "numero": "100", "complemento": null },
              "racaCor": { "racaCorDbEnum": "SEM_INFORMACAO" }
            }
            """)!;

        var ficha = FichaExterna.DoEsus(EsusPecCliente.FichaDe(json));

        ficha.Endereco!.Logradouro.Should().Be("AVENIDA ROBERTO SILVEIRA");
        ficha.Endereco.Uf.Should().Be("RJ");
        ficha.NomePai.Should().Be("JOSE DA SILVA");
        ficha.Email.Should().Be("maria@exemplo.com");
        ficha.RacaCor.Should().BeNull("SEM_INFORMACAO não é dado para completar a ficha");
        ficha.Telefones.Should().ContainSingle("99999-9999 é placeholder de recepção, não telefone")
            .Which.Numero.Should().Be("21998765432");
        ficha.Avisos.Should().Contain(a => a.Contains("óbito"));
        ficha.AtualizadoEm.Should().NotBeNull();
    }

    [Theory]
    [InlineData("UsuarioJaLogadoException", "Login: qualquer coisa", true)]
    [InlineData("PecAuthenticationException", "Login: Você já está logado em outra sessão. Deseja continuar nesta aqui?", true)]
    [InlineData("PecAuthenticationException", "Login: Usuário e/ou senha inválidos", false)]
    public void Login_do_esus_separa_outra_sessao_aberta_de_senha_errada(string classificacao, string mensagem, bool outraSessao) =>
        EsusPecCliente.OutraSessao(new ErroEsusPec(mensagem, classificacao)).Should().Be(outraSessao);

    // ---------------------------------------------------------------- comparação

    private static PacienteDto Ficha(
        string cpf = Cpf, string? cns = null, string nome = "MARIA DA SILVA", DateOnly? nascimento = null,
        string? mae = null, EnderecoDto? endereco = null, Sexo sexo = Sexo.NaoInformado) =>
        new(PacienteId, nome, cpf, cns, 0, 0, true, DateTime.UtcNow,
            null, nascimento ?? new DateOnly(1980, 3, 5), sexo, EstadoCivil.NaoInformado, RacaCor.NaoInformado,
            Escolaridade.NaoInformado, null, null, "Brasileira", mae, null, null, endereco,
            "21911112222", null, null, null, null, null, null, TipoSanguineo.NaoInformado, FatorRh.NaoInformado,
            [], [], [], [], null, null, null);

    private static FichaExterna Fonte(
        string? cpf = Cpf, string? cns = null, string? nome = "MARIA DA SILVA", DateOnly? nascimento = null,
        string? mae = null, EnderecoDto? endereco = null, Sexo? sexo = null, params TelefoneExterno[] telefones) =>
        new(FichaExterna.Cadsus, cpf, cns, nome, null, nascimento ?? new DateOnly(1980, 3, 5), sexo, mae, null, null,
            null, endereco, telefones, [], null);

    [Fact]
    public void Vazio_na_ficha_completa_diferente_diverge_e_igual_so_conta()
    {
        var r = ComparadorFicha.Comparar(
            Ficha(mae: null, sexo: Sexo.Masculino),
            [],
            Fonte(mae: "JOANA DA SILVA", sexo: Sexo.Feminino, nome: "Maria  da Silva"));

        r.Bloqueado.Should().BeFalse();
        r.Campos.Single(c => c.Campo == "nomeMae").Situacao.Should().Be(SituacaoCampoFicha.Completar);
        r.Campos.Single(c => c.Campo == "sexo").Situacao.Should().Be(SituacaoCampoFicha.Divergente);
        r.Campos.Should().NotContain(c => c.Campo == "nome", "acento, caixa e espaço não são divergência");
        r.CamposIguais.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Campo_vazio_na_fonte_nunca_vira_proposta_de_apagar()
    {
        var r = ComparadorFicha.Comparar(Ficha(mae: "JOANA", cns: "700123456789012"), [], Fonte(mae: null, cns: null));
        r.Campos.Should().NotContain(c => c.Campo == "nomeMae" || c.Campo == "cns");
    }

    [Fact]
    public void CPF_diferente_na_fonte_bloqueia_tudo()
    {
        var r = ComparadorFicha.Comparar(Ficha(), [], Fonte(cpf: CpfOutro, mae: "JOANA"));
        r.Bloqueado.Should().BeTrue();
        r.Avisos[0].Should().Contain("outra pessoa");
    }

    [Fact]
    public void CPF_so_se_oferece_a_quem_nao_tem()
    {
        var r = ComparadorFicha.Comparar(Ficha(cpf: ""), [], Fonte());
        r.Campos.Single(c => c.Campo == "cpf").Situacao.Should().Be(SituacaoCampoFicha.Completar);
    }

    [Fact]
    public void Nome_e_nascimento_diferentes_passam_pela_Receita()
    {
        var r = ComparadorFicha.Comparar(Ficha(), [], Fonte(nome: "MARIA SILVA SANTOS", nascimento: new DateOnly(1980, 5, 3)));
        r.Campos.Where(c => c.Campo is "nome" or "dataNascimento").Should().HaveCount(2)
            .And.OnlyContain(c => c.ConfereNaReceita);
    }

    [Fact]
    public void Telefone_que_a_ficha_ja_tem_mesmo_no_historico_ou_com_DDI_nao_e_oferecido()
    {
        var r = ComparadorFicha.Comparar(Ficha(), ["5521998765432", "2126371234"],
            Fonte(telefones:
            [
                new TelefoneExterno("21998765432", "celular", "Celular"),
                new TelefoneExterno("2126371234", "residencial", "Residencial"),
                new TelefoneExterno("21977776666", "celular", "Contato"),
            ]));

        r.Telefones.Should().ContainSingle().Which.Numero.Should().Be("21977776666");
    }

    [Fact]
    public void Endereco_igual_a_menos_do_complemento_nao_e_divergencia()
    {
        var nosso = new EnderecoDto("24900-000", "Rua das Flores", "10", "casa 2", "Centro", "Maricá", "RJ", null);
        var deLa = new EnderecoDto("24900000", "RUA DAS FLORES", "10", null, "CENTRO", "MARICA", "RIO DE JANEIRO", null);
        var r = ComparadorFicha.Comparar(Ficha(endereco: nosso), [], Fonte(endereco: deLa));
        r.Campos.Should().NotContain(c => c.Campo == "endereco");
    }

    // ---------------------------------------------------------------- gravação

    private sealed class Cenario
    {
        public IPacienteFhirClient Hub { get; } = Substitute.For<IPacienteFhirClient>();
        public IPacientesService Pacientes { get; } = Substitute.For<IPacientesService>();
        public ISerNovaSolicitacaoService Ser { get; } = Substitute.For<ISerNovaSolicitacaoService>();
        public IConsultaFichaEsusPec Esus { get; } = Substitute.For<IConsultaFichaEsusPec>();
        public IIntegracaoCredencialService Credenciais { get; } = Substitute.For<IIntegracaoCredencialService>();
        public IConsultaCpfService Receita { get; } = Substitute.For<IConsultaCpfService>();
        public IAuditoriaService Auditoria { get; } = Substitute.For<IAuditoriaService>();
        public MemoryCache Cache { get; } = new(new MemoryCacheOptions());

        public Cenario(Patient? patient = null)
        {
            var p = patient ?? Paciente();
            Hub.ObterAsync(PacienteId, Arg.Any<CancellationToken>()).Returns(p);
            Pacientes.ObterPorIdAsync(PacienteId, Arg.Any<CancellationToken>())
                .Returns(_ => PacienteFhirMapperParaTeste.ParaDto(p));
            Pacientes.AdicionarTelefoneAsync(PacienteId, Arg.Any<AdicionarTelefoneRequest>(), Arg.Any<CancellationToken>())
                .Returns(true);
        }

        public EnriquecimentoPacienteService Servico(Guid? usuario = null)
        {
            // A conexão nunca é aberta: sem usuário, o acesso global responde "não" sem ir ao banco.
            var db = new SmsMaisDbContext(new DbContextOptionsBuilder<SmsMaisDbContext>()
                .UseNpgsql("Host=localhost;Database=nao-usado").Options);
            return new EnriquecimentoPacienteService(
                Hub, Pacientes, Ser, Esus, Credenciais, Receita, Auditoria,
                new UsuarioAtualAccessorFake(usuario ?? Operador), db, Cache,
                NullLogger<EnriquecimentoPacienteService>.Instance);
        }

        public void SerDevolve(params SerCampoPacienteDto[] campos) =>
            Ser.PesquisarPacienteAsync(Cpf, Arg.Any<CancellationToken>())
                .Returns(new SerPacienteEncontradoDto(true, [], campos));
    }

    private static Patient Paciente(string? mae = null) => new()
    {
        Id = PacienteId.ToString(),
        Active = true,
        BirthDate = "1980-03-05",
        Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "MARIA DA SILVA" }],
        Identifier = [new Identifier(PatientMergeFhir.SystemCpf, Cpf)],
        Telecom = [new ContactPoint(ContactPoint.ContactPointSystem.Phone, null, "21911112222")],
        Contact = mae is null ? [] :
        [
            new Patient.ContactComponent
            {
                Relationship = [new CodeableConcept("http://terminology.hl7.org/CodeSystem/v2-0131", "MTH")],
                Name = new HumanName { Text = mae },
            },
        ],
    };

    private static SerCampoPacienteDto[] PainelBasico(string nome = "MARIA DA SILVA", string nascimento = "05/03/1980") =>
    [
        Campo("form0:nome", "Nome", nome),
        Campo("form0:cpf", "CPF", Cpf),
        Campo("form0:dataNascimento", "Data de Nascimento", nascimento),
        Campo("form0:nomeMae", "Nome da Mãe", "JOANA DA SILVA"),
        Campo("form0:j_id178", "Telefone WhatsApp", "(21) 99876-5432"),
    ];

    [Fact]
    public async Task Grava_so_o_escolhido_pelo_caminho_da_edicao_e_acrescenta_telefone_com_origem()
    {
        var c = new Cenario();
        c.SerDevolve(PainelBasico());
        var servico = c.Servico();

        var consulta = await servico.ConsultarCadsusAsync(PacienteId);
        consulta.ConsultaId.Should().NotBeNull();
        consulta.Telefones.Should().ContainSingle();

        var r = await servico.AplicarAsync(PacienteId,
            new AplicarEnriquecimentoRequest(consulta.ConsultaId!.Value, ["nomeMae"], ["21998765432"]));

        r.Gravados.Should().Equal("Nome da mãe");
        r.TelefonesAcrescentados.Should().Equal("(21) 99876-5432");
        await c.Pacientes.Received(1).AtualizarAsync(PacienteId,
            Arg.Is<AtualizarPacienteRequest>(x => x.NomeDaMae == "JOANA DA SILVA" && x.TelefonePrincipal == "21911112222"),
            Arg.Any<CancellationToken>());
        await c.Pacientes.Received(1).AdicionarTelefoneAsync(PacienteId,
            Arg.Is<AdicionarTelefoneRequest>(t => t.Numero == "21998765432" && t.Origem == "cadsus" && t.Tipo == "celular"),
            Arg.Any<CancellationToken>());
        await c.Receita.DidNotReceiveWithAnyArgs().ConsultarCpfAsync(default!, default);
        await c.Auditoria.Received(1).RegistrarAsync("Paciente", PacienteId.ToString(), "EnriquecimentoCadsus",
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        // A consulta é de uso único: gravar de novo com a mesma chave não regrava.
        var deNovo = () => servico.AplicarAsync(PacienteId, new AplicarEnriquecimentoRequest(consulta.ConsultaId!.Value, ["nomeMae"]));
        await deNovo.Should().ThrowAsync<ValidacaoException>().Where(e => e.Erros.ContainsKey("enriquecimento.consulta_expirada"));
    }

    [Fact]
    public async Task Receita_que_nao_confirma_o_nascimento_barra_antes_de_qualquer_escrita()
    {
        var c = new Cenario();
        c.SerDevolve(PainelBasico(nascimento: "03/05/1980"));
        c.Receita.ConsultarCpfAsync(Cpf, new DateOnly(1980, 5, 3), Arg.Any<CancellationToken>())
            .Throws(new ValidacaoException("proxy.cpf.nao_encontrado", "Data de nascimento não confere."));
        var servico = c.Servico();

        var consulta = await servico.ConsultarCadsusAsync(PacienteId);
        var gravar = () => servico.AplicarAsync(PacienteId,
            new AplicarEnriquecimentoRequest(consulta.ConsultaId!.Value, ["dataNascimento", "nomeMae"], ["21998765432"]));

        await gravar.Should().ThrowAsync<ValidacaoException>().Where(e => e.Erros.ContainsKey("enriquecimento.receita_nao_confere"));
        await c.Pacientes.DidNotReceiveWithAnyArgs().CorrigirNascimentoAsync(default, default);
        await c.Pacientes.DidNotReceiveWithAnyArgs().AtualizarAsync(default, default!);
        await c.Pacientes.DidNotReceiveWithAnyArgs().AdicionarTelefoneAsync(default, default!);
    }

    [Fact]
    public async Task Nome_que_a_Receita_da_diferente_da_fonte_nao_e_gravado()
    {
        var c = new Cenario();
        c.SerDevolve(PainelBasico(nome: "MARIA SILVA"));
        c.Receita.ConsultarCpfAsync(Cpf, new DateOnly(1980, 3, 5), Arg.Any<CancellationToken>())
            .Returns(new HubCpfRespostaDto(Cpf, "MARIA DA SILVA", "05/03/1980", "REGULAR"));
        var servico = c.Servico();

        var consulta = await servico.ConsultarCadsusAsync(PacienteId);
        var gravar = () => servico.AplicarAsync(PacienteId, new AplicarEnriquecimentoRequest(consulta.ConsultaId!.Value, ["nome"]));

        await gravar.Should().ThrowAsync<ValidacaoException>().Where(e => e.Erros.ContainsKey("enriquecimento.receita_nome_diferente"));
        await c.Pacientes.DidNotReceiveWithAnyArgs().AtualizarNomeAsync(default, default!);
    }

    [Fact]
    public async Task Nome_confirmado_pela_Receita_vai_pelo_endpoint_do_nome()
    {
        var c = new Cenario();
        c.SerDevolve(PainelBasico(nome: "MARIA DA SILVA SANTOS"));
        c.Receita.ConsultarCpfAsync(Cpf, new DateOnly(1980, 3, 5), Arg.Any<CancellationToken>())
            .Returns(new HubCpfRespostaDto(Cpf, "Maria da Silva Santos", "05/03/1980", "REGULAR"));
        var servico = c.Servico();

        var consulta = await servico.ConsultarCadsusAsync(PacienteId);
        await servico.AplicarAsync(PacienteId, new AplicarEnriquecimentoRequest(consulta.ConsultaId!.Value, ["nome"]));

        await c.Pacientes.Received(1).AtualizarNomeAsync(PacienteId,
            Arg.Is<AtualizarNomePacienteRequest>(x => x.NomeCompleto == "MARIA DA SILVA SANTOS"), Arg.Any<CancellationToken>());
        await c.Pacientes.DidNotReceiveWithAnyArgs().AtualizarAsync(default, default!);
    }

    [Fact]
    public async Task Consulta_de_outro_operador_nao_serve_para_gravar()
    {
        var c = new Cenario();
        c.SerDevolve(PainelBasico());
        var consulta = await c.Servico().ConsultarCadsusAsync(PacienteId);

        var gravar = () => c.Servico(Guid.NewGuid()).AplicarAsync(PacienteId,
            new AplicarEnriquecimentoRequest(consulta.ConsultaId!.Value, ["nomeMae"]));

        await gravar.Should().ThrowAsync<ValidacaoException>().Where(e => e.Erros.ContainsKey("enriquecimento.consulta_expirada"));
    }

    [Fact]
    public async Task CPF_que_ja_e_de_outro_cadastro_nao_entra()
    {
        var semCpf = Paciente();
        semCpf.Identifier = [new Identifier(PatientMergeFhir.SystemCns, "700123456789012")];
        var c = new Cenario(semCpf);
        c.Ser.PesquisarPacienteAsync("700123456789012", Arg.Any<CancellationToken>())
            .Returns(new SerPacienteEncontradoDto(true, [], PainelBasico()));
        c.Pacientes.ObterPorCpfAsync(Cpf, Arg.Any<CancellationToken>())
            .Returns(new PacienteExistenciaDto(Guid.NewGuid(), "MARIA DA SILVA", Cpf, true));
        var servico = c.Servico();

        var consulta = await servico.ConsultarCadsusAsync(PacienteId);
        consulta.ConsultadoPor.Should().Be("CNS");
        var gravar = () => servico.AplicarAsync(PacienteId,
            new AplicarEnriquecimentoRequest(consulta.ConsultaId!.Value, ["cpf", "nomeMae"]));

        await gravar.Should().ThrowAsync<ConflitoException>().Where(e => e.Codigo == "enriquecimento.cpf_de_outro_cadastro");
        await c.Pacientes.DidNotReceiveWithAnyArgs().DefinirCpfAsync(default, default!);
        await c.Pacientes.DidNotReceiveWithAnyArgs().AtualizarAsync(default, default!);
    }

    [Fact]
    public async Task Conta_da_plataforma_no_esus_so_para_quem_tem_acesso_global()
    {
        var c = new Cenario();
        // Sem usuário no token: AcessoGlobalUsuario responde "não" sem consultar o banco.
        var servico = new EnriquecimentoPacienteService(
            c.Hub, c.Pacientes, c.Ser, c.Esus, c.Credenciais, c.Receita, c.Auditoria,
            new UsuarioAtualAccessorFake(), new SmsMaisDbContext(new DbContextOptionsBuilder<SmsMaisDbContext>()
                .UseNpgsql("Host=localhost;Database=nao-usado").Options),
            c.Cache, NullLogger<EnriquecimentoPacienteService>.Instance);

        var consultar = () => servico.ConsultarEsusAsync(PacienteId, new ConsultarEsusRequest());

        await consultar.Should().ThrowAsync<ValidacaoException>().Where(e => e.Erros.ContainsKey("esus.senha_obrigatoria"));
        await c.Esus.DidNotReceiveWithAnyArgs().ConsultarAsync(default!, default!, default);
    }

    [Fact]
    public async Task Esus_com_outra_sessao_aberta_pergunta_antes_de_derrubar()
    {
        var c = new Cenario();
        c.Esus.ConsultarAsync(Arg.Is<ContaEsusPec>(x => !x.Forcar), Cpf, Arg.Any<CancellationToken>())
            .Returns(new ResultadoConsultaEsusPec(true, null));
        var servico = c.Servico();

        var consultar = () => servico.ConsultarEsusAsync(PacienteId, new ConsultarEsusRequest("123.456.789-00", "segredo"));

        await consultar.Should().ThrowAsync<ConflitoException>().Where(e => e.Codigo == "esus.sessao_aberta");
    }

    [Fact]
    public async Task Senha_errada_no_esus_volta_com_a_mensagem_do_PEC()
    {
        var c = new Cenario();
        c.Esus.ConsultarAsync(Arg.Any<ContaEsusPec>(), Cpf, Arg.Any<CancellationToken>())
            .Throws(new ErroEsusPec("Login: Usuário e/ou senha inválidos", "PecAuthenticationException"));
        var servico = c.Servico();

        var consultar = () => servico.ConsultarEsusAsync(PacienteId, new ConsultarEsusRequest("12345678900", "errada"));

        (await consultar.Should().ThrowAsync<ValidacaoException>().Where(e => e.Erros.ContainsKey("esus.login_recusado")))
            .Which.Message.Should().Contain("senha inválidos").And.NotContain("errada");
    }

    // ---------------------------------------------------------------- gravações novas na ficha

    [Fact]
    public void Trocar_CNS_principal_guarda_o_anterior_como_antigo_e_poe_o_novo_primeiro()
    {
        var p = new Patient
        {
            Identifier =
            [
                new Identifier(PatientMergeFhir.SystemCpf, Cpf),
                new Identifier(PatientMergeFhir.SystemCns, "898001234567890"),
            ],
        };

        PacienteFhirMapperParaTeste.TrocarCnsPrincipal(p, "700123456789012");

        var cns = p.Identifier.Where(i => i.System == PatientMergeFhir.SystemCns).ToList();
        cns.Should().HaveCount(2);
        cns[0].Value.Should().Be("700123456789012");
        cns[0].Use.Should().Be(Identifier.IdentifierUse.Official);
        cns[1].Value.Should().Be("898001234567890");
        cns[1].Use.Should().Be(Identifier.IdentifierUse.Old);
        PacienteFhirMapperParaTeste.ParaDto(WithId(p)).Cns.Should().Be("700123456789012");
    }

    [Fact]
    public void Nascimento_corrigido_vence_o_reimport_do_PEP()
    {
        var atual = new Patient { BirthDate = "1980-03-05" };
        PacienteFhirMapperParaTeste.AplicarNascimento(atual, new DateOnly(1980, 5, 3));
        var doPep = new Patient { BirthDate = "1980-03-05" };

        PatientMergeFhir.PreservarDoExistente(doPep, atual);

        doPep.BirthDate.Should().Be("1980-05-03");
    }

    private static Patient WithId(Patient p)
    {
        p.Id = PacienteId.ToString();
        return p;
    }
}

/// <summary>Ponte para o mapper (interno ao Core, visível aos testes).</summary>
internal static class PacienteFhirMapperParaTeste
{
    public static PacienteDto ParaDto(Patient p) => PacienteFhirMapper.ParaDto(p);
    public static void TrocarCnsPrincipal(Patient p, string cns) => PacienteFhirMapper.TrocarCnsPrincipal(p, cns);
    public static void AplicarNascimento(Patient p, DateOnly d) => PacienteFhirMapper.AplicarNascimento(p, d);
}
