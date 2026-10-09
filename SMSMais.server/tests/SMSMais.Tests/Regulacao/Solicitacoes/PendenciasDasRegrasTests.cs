using SMSMais.Core.Regulacao.Regras;
using SMSMais.Core.Regulacao.Solicitacoes;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Tests.Regulacao.Solicitacoes;

/// <summary>
/// O que as regras do manual seguram no envio à pré-regulação (09/10/2026). Testes puros, passando
/// pelo avaliador de verdade — é a combinação dos dois que o envio usa.
///
/// <para>O que prendem: pergunta capaz de bloquear e <b>sem resposta</b> trava; <b>"Não sei" não
/// trava</b> (a tela promete isso); pergunta de outro sistema e pergunta de ressalva não seguram o
/// pedido; e destino barrado pela regra trava com o motivo.</para>
/// </summary>
public class PendenciasDasRegrasTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 9);

    private static readonly PacienteParaRegras Adulto = new(new DateOnly(1980, 1, 1), "F", "12345678909", null);

    private static RegulacaoRegra Pergunta(
        SeveridadeRegraRegulacao severidade = SeveridadeRegraRegulacao.Bloqueia,
        SistemaRegulacao? sistema = SistemaRegulacao.Ser) => new()
        {
            Id = Guid.NewGuid(),
            ProcedimentoId = Guid.NewGuid(),
            Tipo = TipoRegraRegulacao.NaoDedutivel,
            Severidade = severidade,
            Sistema = sistema,
            Descricao = "Critérios de exclusão: Doenças psiquiátricas descompensada.",
            Pergunta = "O paciente tem doença psiquiátrica descompensada?",
            RespostaBloqueia = RespostaRegraRegulacao.Sim,
        };

    private static IReadOnlyList<PendenciaEnvioDto> Pendencias(
        IReadOnlyList<RegulacaoRegra> regras,
        SistemaRegulacao? destino,
        Dictionary<Guid, RespostaRegraRegulacao>? respostas = null,
        PacienteParaRegras? paciente = null)
    {
        var avaliacao = AvaliadorElegibilidade.Avaliar(
            regras, paciente ?? Adulto, null, respostas ?? [], [],
            destino is { } d ? [d] : [SistemaRegulacao.Ser, SistemaRegulacao.Sernit],
            Hoje, NaoSeiViraRegulacao.Pendencia);
        return PendenciasDasRegras.De(avaliacao, destino);
    }

    [Fact]
    public void Pergunta_que_bloqueia_sem_resposta_trava_o_envio()
    {
        var regra = Pergunta();

        var p = Pendencias([regra], SistemaRegulacao.Ser);

        p.Should().ContainSingle();
        p[0].Codigo.Should().Be($"regra.{regra.Id}");
        p[0].Descricao.Should().Be("Responda no passo Regras: O paciente tem doença psiquiátrica descompensada?");
    }

    [Fact]
    public void Pergunta_respondida_nao_trava()
    {
        var regra = Pergunta();

        Pendencias([regra], SistemaRegulacao.Ser, new() { [regra.Id] = RespostaRegraRegulacao.Nao })
            .Should().BeEmpty();
    }

    [Fact]
    public void Nao_sei_nao_trava_mesmo_quando_vira_pendencia_para_o_agente()
    {
        var regra = Pergunta();

        Pendencias([regra], SistemaRegulacao.Ser, new() { [regra.Id] = RespostaRegraRegulacao.NaoSei })
            .Should().BeEmpty();
    }

    [Fact]
    public void Pergunta_de_outro_sistema_nao_segura_o_pedido()
    {
        Pendencias([Pergunta(sistema: SistemaRegulacao.Sernit)], SistemaRegulacao.Ser).Should().BeEmpty();
    }

    [Fact]
    public void Pergunta_sem_sistema_vale_para_qualquer_destino()
    {
        Pendencias([Pergunta(sistema: null)], SistemaRegulacao.Sisreg).Should().ContainSingle();
    }

    [Fact]
    public void Pergunta_de_ressalva_sem_resposta_nao_trava()
    {
        Pendencias([Pergunta(SeveridadeRegraRegulacao.Ressalva)], SistemaRegulacao.Ser).Should().BeEmpty();
    }

    [Fact]
    public void Resposta_que_bloqueia_trava_com_o_destino_e_o_motivo()
    {
        var regra = Pergunta();

        var p = Pendencias([regra], SistemaRegulacao.Ser, new() { [regra.Id] = RespostaRegraRegulacao.Sim });

        p.Should().ContainSingle();
        p[0].Codigo.Should().Be("regra.bloqueio");
        p[0].Descricao.Should().StartWith("Pelas regras do manual, este pedido não pode ir para o SER:");
    }

    [Fact]
    public void Idade_fora_da_regra_trava_sem_ninguem_responder_nada()
    {
        // Polissonografia (CRECE p.42): "Menores de 18 anos" — o sistema decide pelo cadastro.
        var menor = new PacienteParaRegras(new DateOnly(2012, 5, 1), "M", "12345678909", null);
        var regra = new RegulacaoRegra
        {
            Id = Guid.NewGuid(),
            ProcedimentoId = Guid.NewGuid(),
            Tipo = TipoRegraRegulacao.Dedutivel,
            Severidade = SeveridadeRegraRegulacao.Bloqueia,
            Sistema = SistemaRegulacao.Ser,
            Descricao = "Critérios de exclusão: Menores de 18 anos.",
            IdadeMinAnos = 18,
        };

        var p = Pendencias([regra], SistemaRegulacao.Ser, paciente: menor);

        p.Should().ContainSingle(x => x.Codigo == "regra.bloqueio")
            .Which.Descricao.Should().Contain("mínimo 18");
    }

    [Fact]
    public void Documento_obrigatorio_fica_com_a_caixinha_e_nao_duplica_aqui()
    {
        var doc = new RegulacaoRegra
        {
            Id = Guid.NewGuid(),
            ProcedimentoId = Guid.NewGuid(),
            Tipo = TipoRegraRegulacao.Documental,
            Severidade = SeveridadeRegraRegulacao.Bloqueia,
            Sistema = SistemaRegulacao.Ser,
            Descricao = "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.",
            DocumentoRotulo = "Encaminhamento médico",
            Obrigatorio = true,
        };

        Pendencias([doc], SistemaRegulacao.Ser).Should().BeEmpty();
    }

    [Fact]
    public void Sem_destino_e_com_todos_barrados_trava_com_o_primeiro_motivo()
    {
        var regra = Pergunta(sistema: null);

        var p = Pendencias([regra], null, new() { [regra.Id] = RespostaRegraRegulacao.Sim });

        p.Should().ContainSingle(x => x.Codigo == "regra.bloqueio")
            .Which.Descricao.Should().StartWith("Pelas regras do manual, este pedido não pode seguir:");
    }
}
