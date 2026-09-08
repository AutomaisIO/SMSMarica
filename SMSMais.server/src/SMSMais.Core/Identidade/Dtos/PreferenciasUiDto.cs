namespace SMSMais.Core.Identidade.Dtos;

/// <summary>
/// Preferências de UI do usuário, persistidas por usuário (jsonb em <c>usuario.preferencias_ui</c>).
/// Campos além de <see cref="MenuDefaults"/> são opcionais: no PUT, cada campo nulo é
/// preservado (merge no servidor), permitindo que telas diferentes gravem só a sua parte.
/// </summary>
/// <param name="MenuDefaults">Tela default de cada seção do menu (id da seção → rota). Anulável de propósito: o PUT aceita payload parcial (ex.: só a altura do composer) sem esbarrar no required implícito do [ApiController]; o merge preserva o valor atual.</param>
/// <param name="AlturaComposerChat">Altura (px) da caixa de digitação do chat de conversas.</param>
/// <param name="EnviarComEnter">Se Enter envia a mensagem no chat (Shift+Enter quebra linha). Quando desligado, Enter também quebra linha.</param>
/// <param name="VerComoSolicitante">Na lista de Solicitações de Exame, ver por padrão a visão de SOLICITANTE (o que a unidade pediu) em vez de EXECUTANTE (o que ela realiza). Configurado uma vez, fica salvo no usuário. Ver ticket #84.</param>
/// <param name="LargurasTabela">Larguras (px) das colunas das tabelas redimensionáveis, por tela: id da tela → (chave da coluna → largura). Ajustadas pelo separador arrastável no cabeçalho e salvas no perfil. Ver ticket #99. O front envia o mapa completo (não parcial), então o merge por campo aqui preserva tudo.</param>
/// <param name="ExamesTipos">Ids dos tipos de exame marcados na tela de Exames de imagem — o recorte fino de quem lauda MG e OT mas não lauda tudo dentro delas. Aplicado pelo servidor (o tipo não existe no DICOM). Lista vazia = todos.</param>
/// <param name="ExamesModalidades">Modalidades DICOM que a tela de Exames de imagem mostra por padrão (ex.: <c>["MG","OT"]</c> para quem lauda mamografia e densitometria). Vira o filtro <c>ModalitiesInStudy</c> do QIDO — recorte de CONVENIÊNCIA, não de segurança: quem limpa a seleção volta a ver todas as modalidades da sua unidade. Lista vazia = sem recorte.</param>
/// <param name="RegulacaoSistemasOcultos">Sistemas reguladores que a tela de Regras de elegibilidade NÃO lista (ex.: <c>["Sisreg"]</c> para quem cuida só do estadual). Recorte de CONVENIÊNCIA: some da listagem, mas as regras do sistema omitido continuam valendo no wizard — quem desmarca volta a ver tudo. Lista vazia = mostra todos.</param>
public sealed record PreferenciasUiDto(
    Dictionary<string, string>? MenuDefaults,
    int? AlturaComposerChat = null,
    bool? EnviarComEnter = null,
    bool? VerComoSolicitante = null,
    Dictionary<string, Dictionary<string, int>>? LargurasTabela = null,
    List<string>? ExamesModalidades = null,
    List<string>? ExamesTipos = null,
    List<string>? RegulacaoSistemasOcultos = null);
