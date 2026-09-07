using System.Text.Json;

using SMSMais.Core.Regulacao.Regras;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Tests.Regulacao.Regras;

/// <summary>
/// A régua clínica do módulo (plano 03). Testes puros — sem banco.
///
/// <para>O que prendem, e por quê: <b>falta de dado não é "atende"</b> (sem nascimento, sem CID,
/// sem resposta, o resultado é `Indefinido` e alguém confere); a <b>ressalva de destino</b>
/// funciona — bloqueado no SER e livre no SERNIT é o caso que o módulo existe para tratar; e
/// regra <b>informativa não vira pergunta</b>, que é o que impede o questionário de nascer com 20
/// itens por procedimento.</para>
/// </summary>
public class AvaliadorElegibilidadeTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 7);

    private static readonly SistemaRegulacao[] Ambos =
        [SistemaRegulacao.Ser, SistemaRegulacao.Sernit];

    private static RegulacaoRegra Regra(
        TipoRegraRegulacao tipo,
        SeveridadeRegraRegulacao severidade = SeveridadeRegraRegulacao.Bloqueia,
        SistemaRegulacao? sistema = null,
        int? idadeMin = null,
        int? idadeMax = null,
        string? sexo = null,
        bool exigeCpf = false,
        string[]? cidsPermitidos = null,
        string[]? cidsExcluidos = null,
        string? pergunta = null,
        RespostaRegraRegulacao? respostaBloqueia = null,
        NaoSeiViraRegulacao? naoSeiVira = null,
        string? documento = null,
        Guid? tipoExameId = null,
        int? validadeDias = null,
        bool obrigatorio = true) => new()
        {
            Id = Guid.NewGuid(),
            ProcedimentoId = Guid.NewGuid(),
            Tipo = tipo,
            Severidade = severidade,
            Sistema = sistema,
            Descricao = "regra de teste",
            IdadeMinAnos = idadeMin,
            IdadeMaxAnos = idadeMax,
            Sexo = sexo,
            ExigeCpf = exigeCpf,
            CidsPermitidosJson = cidsPermitidos is null ? null : JsonSerializer.Serialize(cidsPermitidos),
            CidsExcluidosJson = cidsExcluidos is null ? null : JsonSerializer.Serialize(cidsExcluidos),
            Pergunta = pergunta,
            RespostaBloqueia = respostaBloqueia,
            NaoSeiVira = naoSeiVira,
            DocumentoRotulo = documento,
            TipoExameId = tipoExameId,
            ValidadeDias = validadeDias,
            Obrigatorio = obrigatorio,
            Versao = 1,
        };

    private static PacienteParaRegras Paciente(
        int? idade = 40, string? sexo = "F", string? cpf = "52998224725") =>
        new(idade is null ? null : Hoje.AddYears(-idade.Value), sexo, cpf, null);

    private static AvaliacaoElegibilidadeDto Avaliar(
        IReadOnlyList<RegulacaoRegra> regras,
        PacienteParaRegras? paciente = null,
        string? cid = null,
        IReadOnlyDictionary<Guid, RespostaRegraRegulacao>? respostas = null,
        IReadOnlyList<ExameParaRegras>? exames = null,
        IReadOnlyList<SistemaRegulacao>? candidatos = null,
        NaoSeiViraRegulacao naoSeiPadrao = NaoSeiViraRegulacao.Ressalva) =>
        AvaliadorElegibilidade.Avaliar(
            regras, paciente ?? Paciente(), cid, respostas ?? new Dictionary<Guid, RespostaRegraRegulacao>(),
            exames ?? [], candidatos ?? Ambos, Hoje, naoSeiPadrao);

    [Fact]
    public void Idade_fora_da_faixa_bloqueia_o_destino()
    {
        var r = Avaliar([Regra(TipoRegraRegulacao.Dedutivel, idadeMin: 50)], Paciente(idade: 40));

        r.DestinosPermitidos.Should().BeEmpty();
        r.BloqueiaEnvio.Should().BeTrue();
        r.MotivosDeBloqueio[SistemaRegulacao.Ser].Should().Contain("mínimo 50");
    }

    [Fact]
    public void Idade_dentro_da_faixa_passa_e_os_limites_sao_inclusivos()
    {
        Avaliar([Regra(TipoRegraRegulacao.Dedutivel, idadeMin: 40, idadeMax: 40)], Paciente(idade: 40))
            .DestinosPermitidos.Should().HaveCount(2);
    }

    [Fact]
    public void Sem_nascimento_a_regra_de_idade_fica_indefinida_e_nao_bloqueia()
    {
        var r = Avaliar([Regra(TipoRegraRegulacao.Dedutivel, idadeMin: 50)], Paciente(idade: null));

        // Falta de dado não é "não atende". Bloquear aqui recusaria pedido por lacuna de
        // cadastro; deixar passar como "atende" mentiria. Fica indefinido e alguém confere.
        r.Regras.Single().Resultado.Should().Be(ResultadoRegraRegulacao.Indefinido);
        r.DestinosPermitidos.Should().HaveCount(2);
    }

    [Fact]
    public void O_CID_casa_por_prefixo()
    {
        // O manual fala em "C50"; a hipótese vem "C50.4".
        Avaliar([Regra(TipoRegraRegulacao.Dedutivel, cidsPermitidos: ["C50"])], cid: "C50.4")
            .DestinosPermitidos.Should().HaveCount(2);

        Avaliar([Regra(TipoRegraRegulacao.Dedutivel, cidsPermitidos: ["C50"])], cid: "I10")
            .DestinosPermitidos.Should().BeEmpty();

        Avaliar([Regra(TipoRegraRegulacao.Dedutivel, cidsExcluidos: ["I10"])], cid: "I10.0")
            .DestinosPermitidos.Should().BeEmpty();
    }

    [Fact]
    public void Bloqueado_num_sistema_e_livre_no_outro_e_a_ressalva_de_destino()
    {
        // O caso que o módulo existe para tratar: o pedido passa, marcado "só pode ir para X".
        var r = Avaliar([
            Regra(TipoRegraRegulacao.Dedutivel, sistema: SistemaRegulacao.Ser, idadeMin: 60),
        ], Paciente(idade: 40));

        r.DestinosPermitidos.Should().ContainSingle().Which.Should().Be(SistemaRegulacao.Sernit);
        r.BloqueiaEnvio.Should().BeFalse("há para onde mandar");
        r.MotivosDeBloqueio.Should().ContainKey(SistemaRegulacao.Ser);
    }

    [Fact]
    public void Pergunta_sem_resposta_fica_pendente_e_trava_se_for_bloqueante()
    {
        var regra = Regra(
            TipoRegraRegulacao.NaoDedutivel, pergunta: "Tem marca-passo?",
            respostaBloqueia: RespostaRegraRegulacao.Sim);

        var r = Avaliar([regra]);

        r.PerguntasPendentes.Should().ContainSingle().Which.Pergunta.Should().Be("Tem marca-passo?");
        r.BloqueiaEnvio.Should().BeTrue();
    }

    [Fact]
    public void Pergunta_de_aviso_sem_resposta_nao_segura_o_pedido()
    {
        var r = Avaliar([
            Regra(TipoRegraRegulacao.NaoDedutivel, severidade: SeveridadeRegraRegulacao.Aviso,
                pergunta: "Usa anticoagulante?", respostaBloqueia: RespostaRegraRegulacao.Sim),
        ]);

        r.PerguntasPendentes.Should().ContainSingle();
        r.BloqueiaEnvio.Should().BeFalse("pergunta que só avisa não pode travar a unidade");
    }

    [Fact]
    public void A_resposta_que_bloqueia_e_a_do_manual_nao_a_afirmativa()
    {
        // Metade das regras do manual bloqueia no "Não" ("não pode estar grávida" x "precisa ter
        // laudo"). Deduzir isso do texto invertia 295 das 1.169 regras no spike e.
        var regra = Regra(
            TipoRegraRegulacao.NaoDedutivel, pergunta: "Tem laudo do cardiologista?",
            respostaBloqueia: RespostaRegraRegulacao.Nao);

        var respostas = new Dictionary<Guid, RespostaRegraRegulacao>
        {
            [regra.Id] = RespostaRegraRegulacao.Sim,
        };
        Avaliar([regra], respostas: respostas).DestinosPermitidos.Should().HaveCount(2);

        respostas[regra.Id] = RespostaRegraRegulacao.Nao;
        Avaliar([regra], respostas: respostas).DestinosPermitidos.Should().BeEmpty();
    }

    [Fact]
    public void Nao_sei_segue_a_configuracao_do_municipio()
    {
        var regra = Regra(
            TipoRegraRegulacao.NaoDedutivel, severidade: SeveridadeRegraRegulacao.Ressalva,
            pergunta: "Já fez o exame antes?", respostaBloqueia: RespostaRegraRegulacao.Nao);
        var respostas = new Dictionary<Guid, RespostaRegraRegulacao>
        {
            [regra.Id] = RespostaRegraRegulacao.NaoSei,
        };

        var comoRessalva = Avaliar([regra], respostas: respostas, naoSeiPadrao: NaoSeiViraRegulacao.Ressalva);
        comoRessalva.DestinosComRessalva.Should().HaveCount(2);
        comoRessalva.BloqueiaEnvio.Should().BeFalse();

        var comoPendencia = Avaliar([regra], respostas: respostas, naoSeiPadrao: NaoSeiViraRegulacao.Pendencia);
        comoPendencia.Regras.Single().Resultado.Should().Be(ResultadoRegraRegulacao.Indefinido);
    }

    [Fact]
    public void A_regra_da_propria_regra_vence_o_padrao_do_municipio()
    {
        var regra = Regra(
            TipoRegraRegulacao.NaoDedutivel, severidade: SeveridadeRegraRegulacao.Ressalva,
            pergunta: "?", respostaBloqueia: RespostaRegraRegulacao.Nao,
            naoSeiVira: NaoSeiViraRegulacao.Ressalva);

        var r = Avaliar(
            [regra],
            respostas: new Dictionary<Guid, RespostaRegraRegulacao> { [regra.Id] = RespostaRegraRegulacao.NaoSei },
            naoSeiPadrao: NaoSeiViraRegulacao.Pendencia);

        r.DestinosComRessalva.Should().HaveCount(2);
    }

    [Fact]
    public void Informativa_nao_vira_pergunta_nem_bloqueia()
    {
        // 83% das regras dos manuais são texto clínico corrido (spike e). Sem este tipo, um
        // recurso com 20 critérios pediria 20 respostas e o questionário morreria.
        var r = Avaliar([
            Regra(TipoRegraRegulacao.Informativa, severidade: SeveridadeRegraRegulacao.Bloqueia),
        ]);

        r.PerguntasPendentes.Should().BeEmpty();
        r.DestinosPermitidos.Should().HaveCount(2);
        r.BloqueiaEnvio.Should().BeFalse();
    }

    [Fact]
    public void Documental_vira_caixinha_e_oferece_o_exame_interno_dentro_da_validade()
    {
        var tipoExame = Guid.NewGuid();
        var dentro = new ExameParaRegras(
            Guid.NewGuid(), tipoExame, Hoje.AddDays(-100), Laudado: true, "Raio-X recente", Guid.NewGuid());
        var fora = new ExameParaRegras(
            Guid.NewGuid(), tipoExame, Hoje.AddDays(-400), Laudado: true, "Raio-X antigo", null);

        var r = Avaliar(
            [Regra(TipoRegraRegulacao.Documental, documento: "Raio-X de tórax",
                tipoExameId: tipoExame, validadeDias: 180)],
            exames: [fora, dentro]);

        var caixinha = r.DocumentosPendentes.Should().ContainSingle().Subject;
        caixinha.Rotulo.Should().Be("Raio-X de tórax");

        // Oferece, não resolve: quem confirma que aquele exame é o pedido é uma pessoa.
        caixinha.ExamesInternosCandidatos.Should().ContainSingle()
            .Which.Id.Should().Be(dentro.Id, "o de 400 dias está fora da validade de 180");
    }

    [Fact]
    public void Documental_sozinha_nao_bloqueia_o_envio()
    {
        // Quem cobra o anexo é a caixinha (a exigência), no momento de enviar para a fila — o
        // motor só diz que ela existe.
        Avaliar([Regra(TipoRegraRegulacao.Documental, documento: "Laudo")])
            .BloqueiaEnvio.Should().BeFalse();
    }

    [Fact]
    public void Sem_destino_sobrando_o_envio_trava()
    {
        var r = Avaliar([
            Regra(TipoRegraRegulacao.Dedutivel, sistema: SistemaRegulacao.Ser, idadeMin: 60),
            Regra(TipoRegraRegulacao.Dedutivel, sistema: SistemaRegulacao.Sernit, idadeMin: 60),
        ], Paciente(idade: 40));

        r.DestinosPermitidos.Should().BeEmpty();
        r.BloqueiaEnvio.Should().BeTrue();
        r.MotivosDeBloqueio.Should().HaveCount(2);
    }

    [Fact]
    public void Regra_com_json_torto_deixa_de_restringir_em_vez_de_derrubar_tudo()
    {
        var regra = Regra(TipoRegraRegulacao.Dedutivel);
        regra.CidsPermitidosJson = "{isto não é uma lista}";

        // Uma regra mal cadastrada não pode impedir a avaliação das outras.
        var acao = () => Avaliar([regra], cid: "I10");
        acao.Should().NotThrow();
        Avaliar([regra], cid: "I10").DestinosPermitidos.Should().HaveCount(2);
    }
}
