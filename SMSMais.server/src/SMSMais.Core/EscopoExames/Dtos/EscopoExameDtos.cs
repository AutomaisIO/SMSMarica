using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.EscopoExames.Dtos;

/// <summary>
/// Uma linha da aba "Exames de imagem" da unidade: o que é o exame, se vai à worklist daqui e para
/// qual aparelho.
///
/// <para><b>Não existe "situação pendente" aqui, de propósito.</b> Um exame desligado é uma decisão
/// legítima da unidade — no CDT, os ecocardiogramas e ecodopplers não devem ir à worklist —, não um
/// cadastro pela metade. A primeira versão marcava 40 linhas do CDT como "a configurar" e
/// transformava configuração correta em alarme; quarenta alarmes falsos ensinam a ignorar a tela.
/// Deixar o destino em branco também é legítimo: com mais de um aparelho na modalidade, quem
/// escolhe a sala é a recepção, na autorização.</para>
/// </summary>
/// <param name="EquipamentosCompativeis">
/// Aparelhos ativos da unidade que atendem a modalidade deste exame. A tela usa para dizer se o
/// destino será deduzido (um) ou escolhido pela recepção (mais de um).
/// </param>
public sealed record EscopoExameItemDto(
    Guid Id,
    Guid TipoExameId,
    string TipoExameNome,
    string? CodigoSisreg,
    ModalidadeDicom ModalidadeDicom,
    Guid UnidadeId,
    string UnidadeNome,
    bool EnviarParaWorklist,
    Guid? EquipamentoId,
    string? EquipamentoNome,
    string? EquipamentoAeTitle,
    int EquipamentosCompativeis,
    bool Ativo);

public sealed record AdicionarEscopoExameRequest(
    Guid TipoExameId,
    Guid UnidadeId,
    bool EnviarParaWorklist = false,
    Guid? EquipamentoId = null);

public sealed record AtualizarEscopoExameRequest(
    bool EnviarParaWorklist,
    Guid? EquipamentoId,
    bool Ativo = true);
