using FluentAssertions;
using SMSMais.Core.Solicitacoes;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// Quem ganha satélite de execução (adendo de 20/10/2026 ao ADR-0021): a categoria diz o que o
/// exame É; a modalidade do tipo diz COMO ele é executado. Imagem sempre; fora dela, só tipo com
/// modalidade DICOM — Holter (ECG) e MAPA (OT) são Gráfico/funcional e vão à worklist.
/// </summary>
public class CategoriaSigtapCriaSateliteTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(ModalidadeDicom.Indefinida)]
    [InlineData(ModalidadeDicom.US)]
    public void Imagem_sempre_tem_satelite(ModalidadeDicom? modalidade) =>
        CategoriaSigtap.CriaSatelite(CategoriaSolicitacao.Imagem, modalidade).Should().BeTrue();

    [Theory]
    [InlineData(ModalidadeDicom.ECG)] // Holter
    [InlineData(ModalidadeDicom.OT)]  // MAPA
    public void Grafico_funcional_com_tipo_de_modalidade_tem_satelite(ModalidadeDicom modalidade) =>
        CategoriaSigtap.CriaSatelite(CategoriaSolicitacao.GraficoFuncional, modalidade).Should().BeTrue();

    [Theory]
    [InlineData(CategoriaSolicitacao.GraficoFuncional, null)]                        // sem tipo
    [InlineData(CategoriaSolicitacao.GraficoFuncional, ModalidadeDicom.Indefinida)] // audiometria, EEG…
    [InlineData(CategoriaSolicitacao.Consulta, null)]
    [InlineData(CategoriaSolicitacao.Laboratorio, ModalidadeDicom.Indefinida)]
    public void Sem_modalidade_fora_da_imagem_nao_tem_satelite(CategoriaSolicitacao categoria, ModalidadeDicom? modalidade) =>
        CategoriaSigtap.CriaSatelite(categoria, modalidade).Should().BeFalse();
}
