using FluentAssertions;
using SMSMais.Core.Solicitacoes;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// Quem ganha satélite de execução (adendo de 20/10/2026 ao ADR-0021): a categoria diz o que o
/// exame É; a configuração diz COMO ele é executado. Imagem sempre; fora dela, só tipo com
/// modalidade DICOM que a unidade executante envia à worklist — Holter (ECG) e MAPA (OT) no CDT.
/// </summary>
public class CategoriaSigtapCriaSateliteTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData(ModalidadeDicom.Indefinida, false)]
    [InlineData(ModalidadeDicom.US, true)]
    public void Imagem_sempre_tem_satelite(ModalidadeDicom? modalidade, bool worklist) =>
        CategoriaSigtap.CriaSatelite(CategoriaSolicitacao.Imagem, modalidade, worklist).Should().BeTrue();

    [Theory]
    [InlineData(ModalidadeDicom.ECG)] // Holter
    [InlineData(ModalidadeDicom.OT)]  // MAPA
    public void Grafico_funcional_com_modalidade_e_worklist_na_unidade_tem_satelite(ModalidadeDicom modalidade) =>
        CategoriaSigtap.CriaSatelite(CategoriaSolicitacao.GraficoFuncional, modalidade, true).Should().BeTrue();

    [Theory]
    [InlineData(CategoriaSolicitacao.GraficoFuncional, null, true)]                        // sem tipo
    [InlineData(CategoriaSolicitacao.GraficoFuncional, ModalidadeDicom.Indefinida, true)] // ECG, audiometria, EEG…
    [InlineData(CategoriaSolicitacao.Consulta, null, false)]
    [InlineData(CategoriaSolicitacao.Laboratorio, ModalidadeDicom.Indefinida, false)]
    public void Sem_modalidade_fora_da_imagem_nao_tem_satelite(
        CategoriaSolicitacao categoria, ModalidadeDicom? modalidade, bool worklist) =>
        CategoriaSigtap.CriaSatelite(categoria, modalidade, worklist).Should().BeFalse();

    [Theory]
    [InlineData(CategoriaSolicitacao.Outro, ModalidadeDicom.MR)]            // ressonância "Outro" na DIMAGEM
    [InlineData(CategoriaSolicitacao.GraficoFuncional, ModalidadeDicom.ECG)] // Holter numa unidade sem aparelho
    public void Modalidade_sem_worklist_na_unidade_nao_tem_satelite(CategoriaSolicitacao categoria, ModalidadeDicom modalidade) =>
        CategoriaSigtap.CriaSatelite(categoria, modalidade, false).Should().BeFalse();
}
