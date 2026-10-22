using SMSMais.Core.Translado.Geracao;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Translado;

/// <summary>
/// Etapa 4: o gerador respeita a condição do paciente — cadeirante na cadeira vai em assento de
/// cadeirante, maca só em ambulância, isolamento é exclusivo, cada acompanhante leva um banco — e
/// escolhe o veículo que menos faz falta aos outros.
/// </summary>
public class AcomodacaoNoVeiculoTests
{
    private static (Veiculo V, List<Assento> Livres) Veiculo(TipoVeiculo tipo, params TipoAssento[] assentos)
    {
        var v = new Veiculo { Id = Guid.NewGuid(), Tipo = tipo, Placa = "SIM0A00" };
        var livres = assentos.Select((t, i) => new Assento { Id = Guid.NewGuid(), Numero = i + 1, Tipo = t }).ToList();
        return (v, livres);
    }

    private static NecessidadeViagem Quem(
        MobilidadeTransporte mobilidade = MobilidadeTransporte.Independente, bool isolamento = false,
        bool alto = false, int acompanhantes = 0) =>
        new(Guid.NewGuid(), mobilidade, isolamento, alto, acompanhantes);

    [Fact]
    public void Cadeirante_na_cadeira_vai_no_assento_de_cadeirante_e_sem_ele_nao_cabe()
    {
        var (van, livres) = Veiculo(TipoVeiculo.Van, TipoAssento.Passageiro, TipoAssento.Cadeirante);
        var ana = Quem(MobilidadeTransporte.CadeiranteVeiculoAdaptado);
        var joao = Quem();

        var lugar = AcomodacaoNoVeiculo.Acomodar(van, livres, [joao, ana])!;

        Assert.Equal(TipoAssento.Cadeirante, lugar[ana.SessaoId].Paciente.Tipo);
        Assert.Equal(TipoAssento.Passageiro, lugar[joao.SessaoId].Paciente.Tipo);
        var (carro, bancos) = Veiculo(TipoVeiculo.Carro, TipoAssento.Passageiro, TipoAssento.Passageiro);
        Assert.Null(AcomodacaoNoVeiculo.Acomodar(carro, bancos, [ana]));
    }

    [Fact]
    public void Quem_anda_nao_ocupa_o_lugar_da_cadeira()
    {
        var (van, livres) = Veiculo(TipoVeiculo.Van, TipoAssento.Cadeirante);
        Assert.Null(AcomodacaoNoVeiculo.Acomodar(van, livres, [Quem()]));
    }

    [Fact]
    public void Maca_so_em_ambulancia_e_uma_por_vez()
    {
        var (ambulancia, livres) = Veiculo(TipoVeiculo.Ambulancia, TipoAssento.Cadeirante, TipoAssento.Passageiro, TipoAssento.Acompanhante);
        var (van, bancos) = Veiculo(TipoVeiculo.Van, TipoAssento.Passageiro, TipoAssento.Cadeirante);
        var rita = Quem(MobilidadeTransporte.Maca, acompanhantes: 1);

        var lugar = AcomodacaoNoVeiculo.Acomodar(ambulancia, livres, [rita])!;

        Assert.Equal(TipoAssento.Cadeirante, lugar[rita.SessaoId].Paciente.Tipo); // o lugar da maca
        Assert.Equal(TipoAssento.Acompanhante, Assert.Single(lugar[rita.SessaoId].Acompanhantes).Tipo);
        Assert.Null(AcomodacaoNoVeiculo.Acomodar(van, bancos, [rita]));
        Assert.Null(AcomodacaoNoVeiculo.Acomodar(ambulancia, livres, [rita, Quem(MobilidadeTransporte.Maca)]));
    }

    [Fact]
    public void Isolamento_vai_sozinho_com_os_acompanhantes()
    {
        var (van, livres) = Veiculo(TipoVeiculo.Van, TipoAssento.Passageiro, TipoAssento.Passageiro, TipoAssento.Acompanhante);
        var isolado = Quem(isolamento: true, acompanhantes: 1);

        Assert.NotNull(AcomodacaoNoVeiculo.Acomodar(van, livres, [isolado]));
        Assert.Null(AcomodacaoNoVeiculo.Acomodar(van, livres, [isolado, Quem()]));
    }

    [Fact]
    public void Dois_acompanhantes_levam_dois_bancos_primeiro_os_de_acompanhante()
    {
        var (van, livres) = Veiculo(TipoVeiculo.Van, TipoAssento.Passageiro, TipoAssento.Acompanhante, TipoAssento.Passageiro);
        var ana = Quem(acompanhantes: 2);

        var lugar = AcomodacaoNoVeiculo.Acomodar(van, livres, [ana])!;

        Assert.Equal([TipoAssento.Acompanhante, TipoAssento.Passageiro], lugar[ana.SessaoId].Acompanhantes.Select(a => a.Tipo));
        Assert.Null(AcomodacaoNoVeiculo.Acomodar(van, livres, [ana, Quem()]));
    }

    [Fact]
    public void Escolha_do_veiculo_poupa_ambulancia_e_adaptado_e_carro_para_quem_nao_sobe_em_alto()
    {
        var (carro, livresCarro) = Veiculo(TipoVeiculo.Carro, TipoAssento.Passageiro);
        var (van, livresVan) = Veiculo(TipoVeiculo.Van, TipoAssento.Passageiro);
        var (adaptada, livresAdaptada) = Veiculo(TipoVeiculo.Van, TipoAssento.Passageiro, TipoAssento.Cadeirante);
        var (ambulancia, livresAmb) = Veiculo(TipoVeiculo.Ambulancia, TipoAssento.Passageiro);
        var comum = Quem();
        var naoSobe = Quem(alto: true);

        Assert.True(AcomodacaoNoVeiculo.Preferencia(van, livresVan, comum)
            < AcomodacaoNoVeiculo.Preferencia(adaptada, livresAdaptada, comum));
        Assert.True(AcomodacaoNoVeiculo.Preferencia(adaptada, livresAdaptada, comum)
            < AcomodacaoNoVeiculo.Preferencia(ambulancia, livresAmb, comum));
        Assert.True(AcomodacaoNoVeiculo.Preferencia(carro, livresCarro, naoSobe)
            < AcomodacaoNoVeiculo.Preferencia(van, livresVan, naoSobe));
        Assert.Equal(0, AcomodacaoNoVeiculo.Preferencia(ambulancia, livresAmb, Quem(MobilidadeTransporte.Maca)));
    }

    [Theory]
    [InlineData(false, 2, 2, 0)] // disse que vai sozinho
    [InlineData(true, 0, 1, 1)]  // confirmou acompanhante, sem lista
    [InlineData(null, 2, 2, 2)]  // dois escolhidos para a viagem
    [InlineData(true, 2, 1, 1)]  // nunca acima do limite do atendimento
    [InlineData(null, 0, 1, 0)]
    public void Quantos_acompanhantes_vao(bool? esperado, int escolhidos, int limite, int esperadoN) =>
        Assert.Equal(esperadoN, AcomodacaoNoVeiculo.AcompanhantesDaViagem(esperado, escolhidos, limite));

    [Fact]
    public void Motivo_diz_o_que_falta()
    {
        Assert.Contains("ambulância", AcomodacaoNoVeiculo.MotivoSemVeiculo(Quem(MobilidadeTransporte.Maca)));
        Assert.Contains("assento de cadeirante", AcomodacaoNoVeiculo.MotivoSemVeiculo(Quem(MobilidadeTransporte.CadeiranteVeiculoAdaptado)));
        Assert.Contains("2 acompanhantes", AcomodacaoNoVeiculo.MotivoSemVeiculo(Quem(acompanhantes: 2)));
    }
}
