using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Notificacoes.Confirmacoes;
using SMSMais.Core.Notificacoes.Confirmacoes.Dtos;
using SMSMais.Data.Entities.Notificacoes;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// Cadência, janela e hora do fechamento do motor de conciliação vivem no banco, com tela — e é a
/// tela que precisa de guarda, porque o número que ela grava vira requisição no SISREG.
///
/// <para>O que estes testes protegem é o <b>fechamento fora da janela</b>. O fechamento relê o dia
/// ANTERIOR inteiro; se cair dentro do horário em que o motor lê o dia CORRENTE, as duas passadas
/// disputam o mesmo orçamento anti-robô — e a do dia corrente, que é a que dá "tempo real", é a
/// que perde. Nada no comportamento grita quando isso acontece: só some cancelamento da vista.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ConciliacaoConfiguravelTests(PostgresFixture fixture)
{
    private ConfirmacaoConfiguracaoService Criar() =>
        new(fixture.CriarDbContext(), new UsuarioAtualAccessorFake(Guid.CreateVersion7()));

    private static SalvarConfirmacaoConfiguracaoRequest Pedido(
        int intervalo = 10, int inicio = 8, int fim = 18, int fechamento = 7) =>
        new("08:00", "18:00", 100, true,
            ConciliacaoIntervaloMinutos: intervalo,
            ConciliacaoHoraInicio: inicio,
            ConciliacaoHoraFim: fim,
            ConciliacaoHoraFechamento: fechamento);

    [Fact]
    public async Task Cadencia_e_janela_validas_sao_gravadas_e_voltam_na_leitura()
    {
        var servico = Criar();

        await servico.SalvarAsync(Pedido(intervalo: 5, inicio: 7, fim: 19, fechamento: 6));

        var depois = await new ConfirmacaoConfiguracaoService(
            fixture.CriarDbContext(), new UsuarioAtualAccessorFake()).ObterAsync();

        Assert.Equal(5, depois.ConciliacaoIntervaloMinutos);
        Assert.Equal(7, depois.ConciliacaoHoraInicio);
        Assert.Equal(19, depois.ConciliacaoHoraFim);
        Assert.Equal(6, depois.ConciliacaoHoraFechamento);
    }

    [Theory]
    [InlineData(0)]   // martelo no SISREG
    [InlineData(121)] // "tempo real" que não é tempo nenhum
    public async Task Intervalo_fora_de_1_a_120_e_recusado(int intervalo)
    {
        var erro = await Assert.ThrowsAsync<ValidacaoException>(
            () => Criar().SalvarAsync(Pedido(intervalo: intervalo)));
        Assert.Contains("confirmacao.conciliacao_intervalo_invalido", erro.Erros.Keys);
    }

    [Fact]
    public async Task Janela_que_termina_antes_de_comecar_e_recusada()
    {
        var erro = await Assert.ThrowsAsync<ValidacaoException>(
            () => Criar().SalvarAsync(Pedido(inicio: 18, fim: 8, fechamento: 7)));
        Assert.Contains("confirmacao.conciliacao_janela_invalida", erro.Erros.Keys);
    }

    /// <summary>
    /// O caso que motivou a guarda: 10h parece uma hora razoável para "fechar o dia anterior", mas
    /// cai no meio do expediente e passa a competir com a leitura do dia corrente.
    /// </summary>
    [Fact]
    public async Task Fechamento_dentro_da_janela_de_leitura_e_recusado()
    {
        var erro = await Assert.ThrowsAsync<ValidacaoException>(
            () => Criar().SalvarAsync(Pedido(inicio: 8, fim: 18, fechamento: 10)));
        Assert.Contains("confirmacao.conciliacao_fechamento_invalido", erro.Erros.Keys);
    }

    [Fact]
    public async Task Fechamento_depois_da_janela_e_aceito()
    {
        await Criar().SalvarAsync(Pedido(inicio: 8, fim: 18, fechamento: 20));

        var depois = await new ConfirmacaoConfiguracaoService(
            fixture.CriarDbContext(), new UsuarioAtualAccessorFake()).ObterAsync();
        Assert.Equal(20, depois.ConciliacaoHoraFechamento);
    }

    /// <summary>
    /// Janela de dia inteiro não deixa hora livre para o fechamento. Sem uma recusa própria, a
    /// regra do "fora da janela" rejeitava as 24 horas possíveis e a aba Regras inteira — lembrete,
    /// horário de envio — ficava impossível de salvar, com o erro apontando o campo errado.
    /// </summary>
    [Fact]
    public async Task Janela_de_dia_inteiro_e_recusada_com_erro_proprio()
    {
        var erro = await Assert.ThrowsAsync<ValidacaoException>(
            () => Criar().SalvarAsync(Pedido(inicio: 0, fim: 24, fechamento: 3)));
        Assert.Contains("confirmacao.conciliacao_janela_dia_inteiro", erro.Erros.Keys);
    }

    /// <summary>
    /// Hora ZERO é o caso que a configuração do EF escondia: com a coluna marcada como gerada pelo
    /// banco, o valor 0 (default do CLR) era OMITIDO do INSERT e o Postgres gravava o default dele
    /// — 8 — sem erro nenhum. Só aparece na primeira gravação da instância, quando a linha
    /// singleton ainda não existe; por isso o teste apaga a linha antes.
    /// </summary>
    [Fact]
    public async Task Hora_zero_sobrevive_ao_primeiro_salvamento_da_instancia()
    {
        await using (var limpeza = fixture.CriarDbContext())
        {
            await limpeza.ConfirmacaoConfiguracoes
                .Where(x => x.Id == ConfirmacaoConfiguracao.IdSingleton)
                .ExecuteDeleteAsync();
        }

        await Criar().SalvarAsync(Pedido(inicio: 0, fim: 23, fechamento: 23));

        var depois = await new ConfirmacaoConfiguracaoService(
            fixture.CriarDbContext(), new UsuarioAtualAccessorFake()).ObterAsync();
        Assert.Equal(0, depois.ConciliacaoHoraInicio);
        Assert.Equal(23, depois.ConciliacaoHoraFim);
        Assert.Equal(23, depois.ConciliacaoHoraFechamento);
    }

    /// <summary>
    /// Campo ausente = manter. Uma aba aberta com o bundle antigo do front manda o PUT sem os
    /// quatro campos; com default fixo no record, ela devolvia calada uma configuração afinada
    /// para 10 min / 8h–18h, dobrando as requisições ao SISREG sem ninguém ter pedido.
    /// </summary>
    [Fact]
    public async Task Pedido_sem_os_campos_da_conciliacao_mantem_o_que_esta_gravado()
    {
        await Criar().SalvarAsync(Pedido(intervalo: 30, inicio: 6, fim: 22, fechamento: 23));

        await Criar().SalvarAsync(new SalvarConfirmacaoConfiguracaoRequest("08:00", "18:00", 100, true));

        var depois = await new ConfirmacaoConfiguracaoService(
            fixture.CriarDbContext(), new UsuarioAtualAccessorFake()).ObterAsync();
        Assert.Equal(30, depois.ConciliacaoIntervaloMinutos);
        Assert.Equal(6, depois.ConciliacaoHoraInicio);
        Assert.Equal(22, depois.ConciliacaoHoraFim);
        Assert.Equal(23, depois.ConciliacaoHoraFechamento);
    }

    /// <summary>
    /// Validação sobre o valor EFETIVO: quem manda só metade da combinação precisa ser barrado
    /// contra a metade que já está gravada, senão a combinação inválida entra pela porta de quem
    /// omitiu o resto.
    /// </summary>
    [Fact]
    public async Task Campo_omitido_e_validado_contra_o_que_ja_esta_gravado()
    {
        await Criar().SalvarAsync(Pedido(inicio: 8, fim: 18, fechamento: 20));

        // Só a hora de fim, movida para 21h: engole o fechamento das 20h, que ninguém mandou.
        var erro = await Assert.ThrowsAsync<ValidacaoException>(
            () => Criar().SalvarAsync(new SalvarConfirmacaoConfiguracaoRequest(
                "08:00", "18:00", 100, true, ConciliacaoHoraFim: 21)));
        Assert.Contains("confirmacao.conciliacao_fechamento_invalido", erro.Erros.Keys);
    }

    /// <summary>O dia fechado só avança — uma passada atrasada não desfaz uma recente.</summary>
    [Fact]
    public async Task Marcar_dia_fechado_nunca_anda_para_tras()
    {
        var servico = Criar();
        await servico.MarcarDiaFechadoAsync(new DateOnly(2026, 9, 18));
        await Criar().MarcarDiaFechadoAsync(new DateOnly(2026, 9, 15));

        var depois = await new ConfirmacaoConfiguracaoService(
            fixture.CriarDbContext(), new UsuarioAtualAccessorFake()).ObterAsync();
        Assert.Equal(new DateOnly(2026, 9, 18), depois.ConciliacaoUltimoDiaFechado);
    }
}
