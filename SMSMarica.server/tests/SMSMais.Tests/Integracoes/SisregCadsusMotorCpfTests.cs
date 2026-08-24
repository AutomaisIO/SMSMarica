using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.Proxy;
using SMSMarica.Core.Integracoes.Proxy.Motores;
using SMSMarica.Core.Integracoes.SisregWeb;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// Motor de fallback do proxy CPF via CADSUS/SISREG: casa/nega pela comparação da data de
/// nascimento com a ficha do SUS (mesma semântica do motor Receita) e converte falhas da
/// sessão SISREG em indisponibilidade (o executor retenta/cai pro próximo motor).
/// </summary>
public class SisregCadsusMotorCpfTests
{
    private static readonly MotorExecucao Cfg = new(null, 10, 1, null);
    private const string Cpf = "03622090731";
    private static readonly DateOnly Nascimento = new(1962, 7, 16);

    private static (SisregCadsusMotorCpf Motor, IConsultaCnsService Cadsus) Criar()
    {
        var cadsus = Substitute.For<IConsultaCnsService>();
        return (new SisregCadsusMotorCpf(cadsus), cadsus);
    }

    private static ConsultaCnsRespostaDto Ficha(DateOnly? nascimento) => new(
        Cns: "700000000000000", Cpf: Cpf, Nome: "FULANA DE TAL",
        Sexo: "Feminino", DataNascimento: nascimento, NomeMae: "MAE DE TAL");

    [Fact]
    public async Task Data_confere_retorna_dto_sem_situacao_cadastral()
    {
        var (motor, cadsus) = Criar();
        cadsus.ConsultarPorCpfAsync(Cpf, Arg.Any<CancellationToken>()).Returns(Ficha(Nascimento));

        var dto = await motor.ConsultarAsync(Cpf, Nascimento, Cfg, CancellationToken.None);

        Assert.Equal("FULANA DE TAL", dto.Nome);
        Assert.Equal("Feminino", dto.Sexo);
        Assert.Equal("16/07/1962", dto.DataNascimento);
        Assert.Null(dto.SituacaoCadastral); // CADSUS não é a Receita
    }

    [Fact]
    public async Task Data_divergente_e_negativa_autoritativa()
    {
        var (motor, cadsus) = Criar();
        cadsus.ConsultarPorCpfAsync(Cpf, Arg.Any<CancellationToken>())
            .Returns(Ficha(new DateOnly(1963, 1, 1)));

        await Assert.ThrowsAsync<MotorNaoEncontrouException>(
            () => motor.ConsultarAsync(Cpf, Nascimento, Cfg, CancellationToken.None));
    }

    [Fact]
    public async Task Ficha_sem_data_aceita_com_a_data_digitada()
    {
        var (motor, cadsus) = Criar();
        cadsus.ConsultarPorCpfAsync(Cpf, Arg.Any<CancellationToken>()).Returns(Ficha(null));

        var dto = await motor.ConsultarAsync(Cpf, Nascimento, Cfg, CancellationToken.None);

        Assert.Equal("16/07/1962", dto.DataNascimento);
    }

    [Fact]
    public async Task Nao_encontrado_no_cadsus_e_negativa()
    {
        var (motor, cadsus) = Criar();
        cadsus.ConsultarPorCpfAsync(Cpf, Arg.Any<CancellationToken>())
            .ThrowsAsync(new NaoEncontradoException("sisreg.paciente_nao_encontrado", "não achou"));

        await Assert.ThrowsAsync<MotorNaoEncontrouException>(
            () => motor.ConsultarAsync(Cpf, Nascimento, Cfg, CancellationToken.None));
    }

    [Fact]
    public async Task Falha_da_sessao_sisreg_e_indisponivel_para_retentar()
    {
        var (motor, cadsus) = Criar();
        cadsus.ConsultarPorCpfAsync(Cpf, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("SISREG fora do ar"));

        await Assert.ThrowsAsync<MotorIndisponivelException>(
            () => motor.ConsultarAsync(Cpf, Nascimento, Cfg, CancellationToken.None));
    }
}
