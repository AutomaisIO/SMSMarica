using SMSMais.Core.Notificacoes.Confirmacoes;
using SMSMais.Data.Entities.Enums;
using static SMSMais.Core.Notificacoes.Confirmacoes.AvisoRemarcacaoService;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// A decisão do aviso de remarcação do SISREG (23/10/2026), sem banco: diante da data nova, o
/// paciente recebe a confirmação de novo, a mensagem que ia sair basta, ou a remarcação fica para a
/// equipe — e a resposta anterior deixa de valer quando era um "confirmado".
/// </summary>
public class AvisoRemarcacaoDecisaoTests
{
    private static readonly DateTime Agora = new(2026, 10, 23, 15, 0, 0, DateTimeKind.Utc);

    private static FatosRemarcacao Fatos(
        bool dataAtual = true,
        bool encerrada = false,
        DateTime? dataNova = null,
        StatusConfirmacaoAgendamento confirmacao = StatusConfirmacaoAgendamento.Confirmada,
        bool unidadeAvisa = true,
        bool humano = false,
        StatusComunicacao? comunicacao = StatusComunicacao.Lida) =>
        new(dataAtual, encerrada, dataNova ?? Agora.AddDays(10), confirmacao, unidadeAvisa, humano, comunicacao);

    [Fact]
    public void Quem_confirmou_a_data_antiga_recebe_a_confirmacao_de_novo_e_a_resposta_reabre()
    {
        var d = Decidir(Fatos(), Agora);

        Assert.Equal(DesfechoRemarcacao.Avisar, d.Desfecho);
        Assert.True(d.ReabreConfirmacao);
    }

    [Theory]
    [InlineData(StatusComunicacao.Enviada)]
    [InlineData(StatusComunicacao.Entregue)]
    [InlineData(StatusComunicacao.Lida)]
    public void Quem_recebeu_a_data_antiga_sem_responder_recebe_a_data_nova(StatusComunicacao status)
    {
        var d = Decidir(Fatos(confirmacao: StatusConfirmacaoAgendamento.Pendente, comunicacao: status), Agora);

        Assert.Equal(DesfechoRemarcacao.Avisar, d.Desfecho);
        Assert.False(d.ReabreConfirmacao); // não havia resposta a desfazer
    }

    [Theory]
    [InlineData(StatusComunicacao.Pendente)]
    [InlineData(StatusComunicacao.AguardandoVerificacaoCadastral)]
    public void Mensagem_que_ainda_nao_levou_a_data_sai_com_a_nova(StatusComunicacao status)
    {
        var d = Decidir(Fatos(confirmacao: StatusConfirmacaoAgendamento.Pendente, comunicacao: status), Agora);

        Assert.Equal(DesfechoRemarcacao.SaiComDataNova, d.Desfecho);
    }

    [Fact]
    public void Sem_mensagem_nenhuma_ainda_a_primeira_ja_vai_com_a_data_nova()
    {
        var d = Decidir(Fatos(confirmacao: StatusConfirmacaoAgendamento.Pendente, comunicacao: null), Agora);

        Assert.Equal(DesfechoRemarcacao.Avisar, d.Desfecho);
    }

    [Fact]
    public void Quem_disse_que_nao_ia_fica_para_a_equipe_e_o_nao_vou_nao_e_apagado()
    {
        var d = Decidir(Fatos(confirmacao: StatusConfirmacaoAgendamento.Cancelada), Agora);

        Assert.Equal(DesfechoRemarcacao.Equipe, d.Desfecho);
        Assert.False(d.ReabreConfirmacao);
        Assert.Contains("não iria", d.Nota);
    }

    [Fact]
    public void Unidade_sem_aviso_por_whatsapp_fica_para_a_equipe_mas_a_confirmacao_antiga_deixa_de_valer()
    {
        var d = Decidir(Fatos(unidadeAvisa: false), Agora);

        Assert.Equal(DesfechoRemarcacao.Equipe, d.Desfecho);
        Assert.True(d.ReabreConfirmacao);
        Assert.Contains("telefone", d.Nota);
    }

    [Fact]
    public void Em_atendimento_humano_o_automatico_nao_fala_com_o_paciente()
    {
        Assert.Equal(DesfechoRemarcacao.Equipe, Decidir(Fatos(humano: true), Agora).Desfecho);
        Assert.Equal(DesfechoRemarcacao.Equipe,
            Decidir(Fatos(comunicacao: StatusComunicacao.SubstituidaPorAtendente), Agora).Desfecho);
    }

    [Theory]
    [InlineData(StatusComunicacao.Falha)]
    [InlineData(StatusComunicacao.SemTelefoneValido)]
    public void Mensagem_anterior_que_nao_chegou_fica_para_a_equipe(StatusComunicacao status)
    {
        Assert.Equal(DesfechoRemarcacao.Equipe, Decidir(Fatos(comunicacao: status), Agora).Desfecho);
    }

    [Fact]
    public void Remarcado_para_hoje_fica_para_a_equipe()
    {
        // 23/10 15:00 UTC = 12:00 em Brasília; 20:00 UTC ainda é hoje lá (17:00).
        var d = Decidir(Fatos(dataNova: Agora.AddHours(5)), Agora);

        Assert.Equal(DesfechoRemarcacao.Equipe, d.Desfecho);
        Assert.Contains("hoje", d.Nota);
    }

    [Fact]
    public void Data_nova_que_ja_passou_ou_solicitacao_encerrada_nao_tem_o_que_avisar()
    {
        Assert.Equal(DesfechoRemarcacao.NadaAAvisar, Decidir(Fatos(dataNova: Agora.AddHours(-1)), Agora).Desfecho);
        Assert.Equal(DesfechoRemarcacao.NadaAAvisar, Decidir(Fatos(encerrada: true), Agora).Desfecho);
    }

    [Fact]
    public void Data_que_nao_e_a_da_solicitacao_fica_para_a_equipe_sem_mexer_na_resposta()
    {
        // Outro procedimento do mesmo pedido: a mensagem automática fala do principal.
        var d = Decidir(Fatos(dataAtual: false), Agora);

        Assert.Equal(DesfechoRemarcacao.Equipe, d.Desfecho);
        Assert.False(d.ReabreConfirmacao);
    }
}
