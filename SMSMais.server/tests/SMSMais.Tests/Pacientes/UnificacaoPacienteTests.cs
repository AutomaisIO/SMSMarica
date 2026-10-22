using Hl7.Fhir.Model;
using NSubstitute;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Pacientes.Unificacao;

using Task = System.Threading.Tasks.Task;

namespace SMSMais.Tests.Pacientes;

/// <summary>
/// Orquestração da unificação de pacientes (<see cref="PacientesService.UnificarAsync"/>): as
/// travas de segurança e a ordem das três metades (demografia no sobrevivente → <c>$merge</c> no
/// hub → repontamento de <c>smsmarica.*</c> → auditoria nos dois cadastros).
/// </summary>
public class UnificacaoPacienteTests
{
    private readonly IPacienteFhirClient _hub = Substitute.For<IPacienteFhirClient>();
    private readonly IRepontadorPacienteService _repontador = Substitute.For<IRepontadorPacienteService>();
    private readonly SMSMais.Core.Auditoria.IAuditoriaService _auditoria =
        Substitute.For<SMSMais.Core.Auditoria.IAuditoriaService>();

    private PacientesService Criar() =>
        new(_hub, _auditoria,
            Substitute.For<SMSMais.Core.Geo.IGeocodificadorService>(),
            _repontador,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PacientesService>.Instance);

    private static Patient Paciente(Guid id, string? cpf = null, string? cns = null, bool ativo = true)
    {
        var ident = new List<Identifier>();
        if (cpf is not null) ident.Add(new Identifier(PatientMergeFhir.SystemCpf, cpf));
        if (cns is not null) ident.Add(new Identifier(PatientMergeFhir.SystemCns, cns));
        return new Patient
        {
            Id = id.ToString(),
            Active = ativo,
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "FULANO DE TESTE" }],
            Identifier = ident,
        };
    }

    private void Hub(Guid id, Patient? p) =>
        _hub.ObterAsync(id, Arg.Any<CancellationToken>()).Returns(p);

    /// <summary>O que o <c>$merge</c> do hub deixa no absorvido: o link para quem ficou.</summary>
    private static Patient UnificadoEm(Patient p, Guid sobrevivente)
    {
        p.Active = false;
        p.Link = [new Patient.LinkComponent
        {
            Other = new ResourceReference($"Patient/{sobrevivente}"),
            Type = Patient.LinkType.ReplacedBy,
        }];
        return p;
    }

    private void FusaoDevolve(Guid s, Guid a) =>
        _hub.FundirAsync(s, a, Arg.Any<CancellationToken>())
            .Returns(new ResultadoFusaoHub(0, 0, 0, 0, 0, 0, 0));

    private void RepontamentoDevolve(Guid s, Guid a, int linhas = 0)
    {
        IReadOnlyList<ContagemRepontamento> r =
            linhas == 0 ? [] : [new ContagemRepontamento("laudo", "paciente_id", linhas)];
        _repontador.RepontarAsync(s, a, Arg.Any<CancellationToken>()).Returns(r);
    }

    /// <summary>Gravação no hub devolve o próprio recurso (o que o retry de edição espera).</summary>
    private void HubGravaEco(Guid id) =>
        _hub.AtualizarAsync(id, Arg.Any<Patient>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<Patient>());

    [Fact]
    public async Task Recusa_unificar_o_mesmo_cadastro()
    {
        var id = Guid.CreateVersion7();
        var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
            Criar().UnificarAsync(new UnificarPacientesRequest(id, id)));
        Assert.Contains("paciente.unificar_mesmo", ex.Erros.Keys);
    }

    [Fact]
    public async Task Recusa_reunificar_cadastro_ja_absorvido_por_outro()
    {
        var s = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        Hub(s, Paciente(s, cpf: "11111111111"));
        Hub(a, UnificadoEm(Paciente(a), Guid.CreateVersion7())); // foi para um TERCEIRO cadastro

        var ex = await Assert.ThrowsAsync<ConflitoException>(() =>
            Criar().UnificarAsync(new UnificarPacientesRequest(s, a)));
        Assert.Equal("paciente.ja_unificado", ex.Codigo);
        await _hub.DidNotReceive().FundirAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repontador.DidNotReceive().RepontarAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Recusa_quando_o_cadastro_que_fica_ja_foi_absorvido()
    {
        var s = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        Hub(s, UnificadoEm(Paciente(s, cpf: "11111111111"), Guid.CreateVersion7()));
        Hub(a, Paciente(a));

        var ex = await Assert.ThrowsAsync<ConflitoException>(() =>
            Criar().UnificarAsync(new UnificarPacientesRequest(s, a)));
        Assert.Equal("paciente.ja_unificado", ex.Codigo);
        await _repontador.DidNotReceive().RepontarAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Retoma_unificacao_interrompida_no_mesmo_cadastro_sem_fundir_de_novo()
    {
        // A fusão no hub passou e o que vinha depois não: o absorvido já aponta para ESTE
        // sobrevivente. Refazer completa o que falta em vez de travar em "já unificado".
        var s = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        Hub(s, Paciente(s, cpf: "11111111111"));
        Hub(a, UnificadoEm(Paciente(a), s));
        RepontamentoDevolve(s, a, linhas: 3);

        var r = await Criar().UnificarAsync(new UnificarPacientesRequest(s, a));

        Assert.Equal(3, r.ReferenciasRepontadas);
        await _hub.DidNotReceive().FundirAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repontador.Received(1).RepontarAsync(s, a, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cadastro_desativado_sem_unificacao_pode_ser_unificado()
    {
        // Excluído (active=false) mas nunca unificado — é justamente o duplicado que alguém já
        // tinha "apagado" deixando laudo e agendamento presos nele.
        var s = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        Hub(s, Paciente(s, cpf: "11111111111"));
        Hub(a, Paciente(a, ativo: false));
        FusaoDevolve(s, a);
        RepontamentoDevolve(s, a);

        await Criar().UnificarAsync(new UnificarPacientesRequest(s, a));

        await _hub.Received(1).FundirAsync(s, a, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Move_o_painel_antes_de_fundir_no_hub()
    {
        // A fusão no hub é a única etapa que não se refaz. Se ela falhar, o que veio antes tem de
        // poder rodar de novo — por isso o repontamento (repetível) vem primeiro.
        var s = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        Hub(s, Paciente(s, cpf: "11111111111"));
        Hub(a, Paciente(a));
        FusaoDevolve(s, a);
        RepontamentoDevolve(s, a);

        await Criar().UnificarAsync(new UnificarPacientesRequest(s, a));

        Received.InOrder(() =>
        {
            _repontador.RepontarAsync(s, a, Arg.Any<CancellationToken>());
            _hub.FundirAsync(s, a, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Recusa_CNS_que_nao_e_de_nenhum_dos_dois_antes_de_escrever()
    {
        var s = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        Hub(s, Paciente(s, cpf: "11111111111", cns: "700000000000001"));
        Hub(a, Paciente(a, cns: "700000000000002"));

        var ex = await Assert.ThrowsAsync<ValidacaoException>(() => Criar().UnificarAsync(
            new UnificarPacientesRequest(s, a, ConfirmaChavesDivergentes: true, CnsFinal: "700000000000009")));

        Assert.Contains("paciente.unificar_cns", ex.Erros.Keys);
        await _hub.DidNotReceive().AtualizarAsync(Arg.Any<Guid>(), Arg.Any<Patient>(), Arg.Any<CancellationToken>());
        await _repontador.DidNotReceive().RepontarAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CNS_escolhido_do_outro_cadastro_vira_o_oficial_depois_da_fusao()
    {
        var s = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        Hub(s, Paciente(s, cpf: "11111111111", cns: "700000000000001"));
        Hub(a, Paciente(a, cns: "700000000000002"));
        FusaoDevolve(s, a);
        RepontamentoDevolve(s, a);
        HubGravaEco(s);

        await Criar().UnificarAsync(new UnificarPacientesRequest(
            s, a, ConfirmaChavesDivergentes: true, CnsFinal: "700000000000002"));

        await _hub.Received().AtualizarAsync(s,
            Arg.Is<Patient>(p => PacienteFhirMapper.CnsDe(p) == "700000000000002"),
            Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            _hub.FundirAsync(s, a, Arg.Any<CancellationToken>());
            _hub.AtualizarAsync(s, Arg.Any<Patient>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Telefone_desligado_do_que_fica_nao_volta_pela_copia_do_cadastro()
    {
        // O DTO lê o telecom e, sem o número lá, cai na cópia (blob). Aposentar só o telecom deixava
        // o residencial desligado aparecendo de novo na ficha.
        var s = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        var sobrevivente = Paciente(s, cpf: "11111111111");
        sobrevivente.Telecom =
        [
            new ContactPoint(ContactPoint.ContactPointSystem.Phone, null, "21911112222") { Rank = 1 },
            new ContactPoint(ContactPoint.ContactPointSystem.Phone, ContactPoint.ContactPointUse.Home, "2126340000"),
        ];
        Hub(s, sobrevivente);
        Hub(a, Paciente(a));
        FusaoDevolve(s, a);
        RepontamentoDevolve(s, a);
        HubGravaEco(s);

        var r = await Criar().UnificarAsync(new UnificarPacientesRequest(
            s, a,
            DadosFinais: new AtualizarPacienteRequest(
                Cns: null, Rg: null, TelefonePrincipal: "21911112222", TelefoneResidencial: "2126340000"),
            TelefonesDescartados: ["2126340000"]));

        Assert.Equal(1, r.TelefonesRetirados);
        var dto = PacienteFhirMapper.ParaDto(sobrevivente); // o mock devolve sempre a mesma instância
        Assert.Null(dto.TelefoneResidencial);
        Assert.Equal("21911112222", dto.TelefonePrincipal);
    }

    [Fact]
    public async Task Telefones_do_absorvido_sao_somados_ao_que_fica()
    {
        var s = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        var sobrevivente = Paciente(s, cpf: "11111111111");
        sobrevivente.Telecom = [new ContactPoint(ContactPoint.ContactPointSystem.Phone, null, "21911112222") { Rank = 1 }];
        var absorvido = Paciente(a);
        absorvido.Telecom = [new ContactPoint(ContactPoint.ContactPointSystem.Phone, null, "21933334444")];
        Hub(s, sobrevivente);
        Hub(a, absorvido);
        FusaoDevolve(s, a);
        RepontamentoDevolve(s, a);
        HubGravaEco(s);

        var r = await Criar().UnificarAsync(new UnificarPacientesRequest(s, a));

        Assert.Equal(1, r.ContatosSomados);
        await _hub.Received().AtualizarAsync(s,
            Arg.Is<Patient>(p => p.Telecom.Any(t => t.Value == "21933334444")
                && p.Telecom.Any(t => t.Value == "21911112222")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Recusa_CPF_divergente_sem_confirmacao()
    {
        var s = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        Hub(s, Paciente(s, cpf: "11111111111"));
        Hub(a, Paciente(a, cpf: "22222222222"));

        var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
            Criar().UnificarAsync(new UnificarPacientesRequest(s, a, ConfirmaChavesDivergentes: false)));
        Assert.Contains("paciente.chaves_divergentes", ex.Erros.Keys);
        await _hub.DidNotReceive().FundirAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repontador.DidNotReceive().RepontarAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CPF_divergente_COM_confirmacao_funde_e_reaponta()
    {
        var s = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        Hub(s, Paciente(s, cpf: "11111111111"));
        Hub(a, Paciente(a, cpf: "22222222222"));
        _hub.FundirAsync(s, a, Arg.Any<CancellationToken>())
            .Returns(new ResultadoFusaoHub(1, 2, 0, 3, 0, 0, 1));
        IReadOnlyList<ContagemRepontamento> repontou = [new ContagemRepontamento("laudo", "paciente_id", 4)];
        _repontador.RepontarAsync(s, a, Arg.Any<CancellationToken>()).Returns(repontou);

        var r = await Criar().UnificarAsync(
            new UnificarPacientesRequest(s, a, ConfirmaChavesDivergentes: true));

        Assert.Equal(4, r.ReferenciasRepontadas);
        Assert.Equal(6, r.ClinicoRepontado); // 2+3+1
        Assert.Equal(1, r.IdentificadoresAbsorvidos);
        await _hub.Received(1).FundirAsync(s, a, Arg.Any<CancellationToken>());
        await _repontador.Received(1).RepontarAsync(s, a, Arg.Any<CancellationToken>());
        // Trilha nos DOIS cadastros.
        await _auditoria.Received().RegistrarAsync(
            "Paciente", s.ToString(), "UnificacaoPaciente", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _auditoria.Received().RegistrarAsync(
            "Paciente", a.ToString(), "UnificacaoPaciente", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Caso_comum_sem_CPF_no_absorvido_funde_sem_exigir_confirmacao()
    {
        // Duplicata típica de importação: o absorvido entrou só com CNS (sem CPF). Não é divergência.
        var s = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        Hub(s, Paciente(s, cpf: "11111111111"));
        Hub(a, Paciente(a, cns: "700123456789012"));
        _hub.FundirAsync(s, a, Arg.Any<CancellationToken>())
            .Returns(new ResultadoFusaoHub(1, 0, 0, 0, 0, 0, 0));
        IReadOnlyList<ContagemRepontamento> vazio = [];
        _repontador.RepontarAsync(s, a, Arg.Any<CancellationToken>()).Returns(vazio);

        var r = await Criar().UnificarAsync(new UnificarPacientesRequest(s, a));

        Assert.Equal(0, r.ReferenciasRepontadas);
        await _hub.Received(1).FundirAsync(s, a, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Todo_alvo_do_repontador_aparece_na_previa_com_o_nome_do_modulo()
    {
        // Tabela nova no repontador sem rótulo aparece na prévia da tela pelo nome cru da tabela.
        var semRotulo = RepontadorPacienteService.Alvos
            .Select(a => a.Tabela)
            .Distinct()
            .Where(t => UnificacaoResumo.AgruparModulos([new ContagemRepontamento(t, "x", 1)])[0].Modulo == t)
            .ToList();

        Assert.Empty(semRotulo);
    }

    private void HubBuscaDevolve(params Patient[] pacientes)
    {
        var bundle = new Bundle { Type = Bundle.BundleType.Searchset };
        foreach (var p in pacientes) bundle.Entry.Add(new Bundle.EntryComponent { Resource = p });
        _hub.BuscarAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(bundle);
    }

    [Fact]
    public async Task Cadastro_unificado_nao_aparece_na_busca_por_nome()
    {
        // FERNANDA (22/10/2026): a lápide sem CPF vinha em 1º lugar na lista, e era ela que se abria.
        var s = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        HubBuscaDevolve(UnificadoEm(Paciente(a), s), Paciente(s, cpf: "11111111111"));

        var r = await Criar().BuscarAsync("FULANO DE TESTE");

        Assert.Equal([s], r.Select(x => x.Id));
    }

    [Fact]
    public async Task Cadastro_unificado_nao_responde_pelo_telefone()
    {
        // O webhook do WhatsApp pega o PRIMEIRO paciente do número: a lápide na frente pendurava a
        // mensagem nova no cadastro desativado.
        var s = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        HubBuscaDevolve(UnificadoEm(Paciente(a), s), Paciente(s, cpf: "11111111111"));

        var achado = await Criar().ObterPorTelefoneAsync("21933334444");
        var lista = await Criar().ListarPorTelefoneAsync("21933334444");

        Assert.Equal(s, achado!.Id);
        Assert.Equal([s], lista.Select(x => x.Id));
    }

    [Fact]
    public void Ficha_do_cadastro_unificado_diz_para_onde_foi()
    {
        var s = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();

        Assert.Equal(s, PacienteFhirMapper.ParaDto(UnificadoEm(Paciente(a), s)).UnificadoEm);
        Assert.Null(PacienteFhirMapper.ParaDto(Paciente(s, cpf: "11111111111")).UnificadoEm);
    }
}
