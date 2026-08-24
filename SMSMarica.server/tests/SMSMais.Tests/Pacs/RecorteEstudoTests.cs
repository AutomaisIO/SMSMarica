using SMSMarica.Core.Pacs;

namespace SMSMais.Tests.Pacs;

/// <summary>
/// A regra de quem vê o quê na tela de Exames. Cada caso aqui é um jeito de errar em produção:
/// esconder da recepção o exame que acabou de chegar, ou mostrar a uma unidade o exame de outra.
///
/// <para>O desenho parte de uma dicotomia: <b>conhecido</b> (tem pedido casado ⇒ unidade e tipo,
/// mesmo que o DICOM tenha sido reescrito e perdido o AE de origem) ou <b>órfão</b> (flutua para
/// todo mundo até alguém associar).</para>
/// </summary>
public class RecorteEstudoTests
{
    private static readonly Guid Cdt = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Cmi = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Usf = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TipoMamografia = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TipoDensitometria = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    private static ContextoEstudo DoCdt(Guid? tipo = null, Guid? solicitante = null) =>
        new(Cdt, solicitante, tipo ?? TipoMamografia);

    [Fact]
    public void Sem_restricao_mostra_tudo()
    {
        Assert.Equal(DecisaoRecorte.Mostra, RecorteEstudo.Decidir(DoCdt(), false, [], []));
        Assert.Equal(DecisaoRecorte.Mostra, RecorteEstudo.Decidir(null, false, [], []));
    }

    [Fact]
    public void Estudo_da_minha_unidade_executante_aparece()
    {
        var d = RecorteEstudo.Decidir(DoCdt(), restritoPorUnidade: true, [Cdt], []);
        Assert.Equal(DecisaoRecorte.Mostra, d);
    }

    [Fact]
    public void Estudo_de_outra_unidade_nao_aparece()
    {
        var d = RecorteEstudo.Decidir(DoCdt(), restritoPorUnidade: true, [Cmi], []);
        Assert.Equal(DecisaoRecorte.Descarta, d);
    }

    /// <summary>A USF que PEDIU vê o exame que ela pediu, mesmo executado em outra unidade.</summary>
    [Fact]
    public void Unidade_solicitante_tambem_ve()
    {
        var d = RecorteEstudo.Decidir(DoCdt(solicitante: Usf), restritoPorUnidade: true, [Usf], []);
        Assert.Equal(DecisaoRecorte.Mostra, d);
    }

    /// <summary>
    /// O caso que derrubou a primeira versão (recorte por AE): o estudo REESCRITO por uma
    /// associação manual perde o AE de origem e ficava invisível. Aqui ele é apenas "conhecido" —
    /// o recorte olha o pedido, não o DICOM, então a reescrita não muda nada.
    /// </summary>
    [Fact]
    public void Estudo_conhecido_aparece_independente_do_dicom()
    {
        var d = RecorteEstudo.Decidir(DoCdt(), restritoPorUnidade: true, [Cdt], []);
        Assert.Equal(DecisaoRecorte.Mostra, d);
    }

    /// <summary>
    /// Regra de produto: sem pedido casado não há unidade, e esconder o órfão o deixaria invisível
    /// justamente para quem precisa associá-lo. Ele "fica voando" para todo mundo.
    /// </summary>
    [Fact]
    public void Orfao_aparece_para_qualquer_unidade()
    {
        Assert.Equal(DecisaoRecorte.Mostra, RecorteEstudo.Decidir(null, true, [Cdt], []));
        Assert.Equal(DecisaoRecorte.Mostra, RecorteEstudo.Decidir(null, true, [Cmi], []));
        Assert.Equal(DecisaoRecorte.Mostra, RecorteEstudo.Decidir(null, true, [Usf], []));
    }

    [Fact]
    public void Tipo_marcado_deixa_passar_so_aquele_tipo()
    {
        Assert.Equal(
            DecisaoRecorte.Mostra,
            RecorteEstudo.Decidir(DoCdt(TipoMamografia), true, [Cdt], [TipoMamografia, TipoDensitometria]));
        Assert.Equal(
            DecisaoRecorte.Descarta,
            RecorteEstudo.Decidir(DoCdt(TipoDensitometria), true, [Cdt], [TipoMamografia]));
    }

    /// <summary>
    /// Com filtro de tipo o órfão sai — ele não tem pedido, logo não tem tipo. Mas sai CONTADO:
    /// a tela avisa quantos ficaram de fora, senão o exame recém-chegado sumiria sem explicação.
    /// </summary>
    [Fact]
    public void Orfao_com_filtro_de_tipo_sai_mas_contado()
    {
        var d = RecorteEstudo.Decidir(null, true, [Cdt], [TipoMamografia]);
        Assert.Equal(DecisaoRecorte.OrfaoOcultoPorTipo, d);
    }

    /// <summary>Exame ainda sem mapeamento de tipo não se disfarça de "qualquer tipo".</summary>
    [Fact]
    public void Conhecido_sem_tipo_nao_passa_no_filtro_de_tipo()
    {
        var semTipo = new ContextoEstudo(Cdt, null, null);
        Assert.Equal(DecisaoRecorte.Descarta, RecorteEstudo.Decidir(semTipo, true, [Cdt], [TipoMamografia]));
        // Sem filtro de tipo, ele aparece normalmente.
        Assert.Equal(DecisaoRecorte.Mostra, RecorteEstudo.Decidir(semTipo, true, [Cdt], []));
    }

    /// <summary>Os dois filtros valem juntos: tipo certo, unidade errada, não passa.</summary>
    [Fact]
    public void Tipo_certo_em_unidade_de_fora_nao_passa()
    {
        var d = RecorteEstudo.Decidir(DoCdt(TipoMamografia), true, [Cmi], [TipoMamografia]);
        Assert.Equal(DecisaoRecorte.Descarta, d);
    }

    /// <summary>Quem enxerga duas unidades vê as duas — o escopo é o conjunto, não a primeira.</summary>
    [Fact]
    public void Escopo_com_varias_unidades_cobre_todas()
    {
        Assert.Equal(DecisaoRecorte.Mostra, RecorteEstudo.Decidir(DoCdt(), true, [Cmi, Cdt], []));
        Assert.Equal(DecisaoRecorte.Mostra, RecorteEstudo.Decidir(new ContextoEstudo(Cmi, null, null), true, [Cmi, Cdt], []));
    }
}
