namespace SMSMais.Core.Cidadao.Dtos;

/// <summary>Passo 1 da entrada (ADR-0078): só CPF e data de nascimento.</summary>
public sealed record IniciarEntradaRequest(string? Cpf, DateOnly DataNascimento);

/// <summary>
/// Um número para onde o código pode ir. O app só vê o <see cref="Final"/> (4 últimos dígitos); o
/// número de verdade fica no servidor. <see cref="Origem"/>: <c>cadastro</c> | <c>esus</c>.
/// </summary>
public sealed record OpcaoEntradaDto(string Id, string Final, string Origem, bool Verificado);

/// <summary>
/// A entrada em andamento: os números possíveis e se dá para procurar outros no e-SUS
/// (<see cref="PodeBuscarEsus"/>). Lista vazia e sem e-SUS = "procure o posto".
/// </summary>
public sealed record EntradaCidadaoDto(
    string DesafioId,
    IReadOnlyList<OpcaoEntradaDto> Opcoes,
    bool PodeBuscarEsus,
    int ValidadeSegundos);

/// <summary>Passo final da entrada: o número escolhido (o <see cref="OpcaoEntradaDto.Id"/>).</summary>
public sealed record EnviarCodigoEntradaRequest(string? OpcaoId);

/// <summary>O que o app deve fazer depois de pedir o código.</summary>
public static class SituacaoLoginCidadao
{
    /// <summary>O código foi enviado: o app segue para a tela do código.</summary>
    public const string Otp = "otp";
}

/// <summary>
/// Resultado de pedir o código. No fluxo normal o código vai só pelo WhatsApp e
/// <see cref="CodigoTeste"/> é null. Ele só é preenchido como <b>fallback</b> quando o
/// WhatsApp não está configurado no servidor (ou <c>Tfd:Otp:ModoTeste=true</c>), para não
/// travar o login — aí o código é exibido na tela. <see cref="TelefoneMascarado"/> traz uma
/// dica do destino (ex.: <c>***-1234</c>) para o usuário conferir.
/// </summary>
public sealed record OtpEmitidoDto(
    bool Enviado,
    string Canal,
    string? CodigoTeste,
    int ValidadeSegundos,
    string? TelefoneMascarado = null,
    string Situacao = SituacaoLoginCidadao.Otp);

public sealed record ValidarOtpRequest(string Cpf, string Codigo);

public sealed record PacienteSessaoDto(Guid Id, string Nome, string Cpf);

public sealed record RespostaLoginPacienteDto(string Token, PacienteSessaoDto Paciente);

/// <summary>Perfil que o cidadão vê/edita de si mesmo no app (subconjunto seguro do paciente FHIR).</summary>
public sealed record PerfilCidadaoDto(
    Guid Id,
    string Nome,
    string? NomeSocial,
    string Cpf,
    string? Cns,
    DateOnly? DataNascimento,
    string? Email,
    string? TelefonePrincipal,
    string? TelefoneCelular,
    string? TelefoneResidencial,
    string? FotoBase64);

public sealed record AtualizarContatoCidadaoRequest(
    string? Email,
    string? TelefonePrincipal,
    string? TelefoneCelular,
    string? TelefoneResidencial);

public sealed record AtualizarFotoCidadaoRequest(string? FotoBase64);

// --- Resumos clínicos do app (shapes estáveis p/ a PWA). Atendimentos vêm do hub FHIR
// (Encounter + Condition + documentos); as viagens do transporte, de ITratamentosService. ---
public sealed record AtendimentoResumoDto(
    Guid Id,
    DateTime Data,
    string Estabelecimento,
    string Profissional,
    string Descricao,
    IReadOnlyList<DocumentoResumoDto> Documentos);
/// <summary>Documento clínico do atendimento (DocumentReference), com HTML já decodificado.</summary>
public sealed record DocumentoResumoDto(Guid Id, string Tipo, DateTime? Data, string ConteudoHtml);
/// <summary>Documento escaneado (DocumentoExame "Salvo") anexado a um exame, visível ao cidadão.</summary>
public sealed record AnexoResumoDto(Guid Id, string Nome, long TamanhoBytes, int? Paginas);

/// <summary>
/// Um exame realizado do paciente (SolicitacaoExame). Traz os documentos escaneados, a
/// disponibilidade de imagens no PACS (para gerar o PDF consolidado) e o laudo assinado, se houver.
/// </summary>
public sealed record ExameResumoDto(
    Guid Id,
    DateTime Data,
    string Nome,
    string Status,
    string? StudyInstanceUID,
    bool TemImagens,
    IReadOnlyList<AnexoResumoDto> Documentos,
    Guid? LaudoId,
    bool LaudoAssinado);

public sealed record LaudoResumoDto(Guid Id, DateTime Data, string Titulo, string Status);

/// <summary>Detalhe completo do agendamento do SISREG (ticket do app). Apesar do nome, vale para
/// qualquer pedido do SISREG — exame de imagem, ECG, consulta… <c>Tipo</c> diz qual ("Consulta" |
/// "Exame") para a tela escolher as palavras.</summary>
public sealed record AgendamentoExameDetalheDto(
    Guid SolicitacaoExameId,
    string TipoExame,
    DateTime? DataAgendada,
    DateOnly? DataSolicitacao,
    DateOnly? DataRegulacao,
    string? UnidadeExecutoraNome,
    string? UnidadeExecutoraEndereco,
    string? UnidadeExecutoraTelefone,
    string? UnidadeSolicitanteNome,
    string? SolicitanteNome,
    string? AccessionNumber,
    string? CodigoSolicitacao,
    string Prioridade,
    string? Observacoes,
    string StatusConfirmacao,
    // Rastro da resposta do paciente (para o ticket): como/quando confirmou ou cancelou.
    DateTime? ConfirmadoEm,
    string? ConfirmadoCanal,
    DateTime? ConfirmacaoCanceladaEm,
    string? MotivoCancelamentoPaciente,
    // A chave de acesso só pode ser vista NO DIA do atendimento (Brasília). Decidido no back;
    // o app só mostra o botão quando isto vem true — e o endpoint recusa mesmo assim.
    bool ChaveAcessoDisponivelHoje,
    string Tipo = "Exame");

/// <summary>Chave de acesso (confirmação do SISREG) entregue ao paciente no dia do exame.</summary>
public sealed record ChaveAcessoCidadaoDto(string Chave, string CodigoSolicitacao);

/// <summary>Consulta ou exame do paciente, projetado para o app — de todas as regulações (SISREG,
/// SER, SERNIT, ESUS de São Gonçalo), em três partes (<c>Momento</c>): <c>"Proximo"</c> (agendado
/// de hoje em diante), <c>"NaFila"</c> (esperando vaga, sem data — <c>InicioEm</c> nulo) e
/// <c>"Passado"</c> (já passou ou foi encerrado). A lista vem nessa ordem.
/// <para>Do SISREG, <c>SolicitacaoExameId</c> é o id público do pedido (o do exame de imagem quando
/// há, senão o da solicitação): abre o ticket e, no próximo ainda sem resposta, habilita os botões
/// Confirmar/Não poderei ir (<c>PodeResponder</c>).</para>
/// <para>Na fila é só "na fila": nunca motivo de pendência, posição ou previsão. <c>Origem</c> diz,
/// em linguagem do cidadão, quem regula ("regulação estadual (SER)").</para>
/// <para><c>TemHora</c> = false: a fonte só deu o dia — a tela não inventa "00:00".</para>
/// <para><c>GuiaDocumentoId</c>: a guia do agendamento do SER/SERNIT/ESUS no acervo do paciente
/// (ADR-0076) — abre pela mesma URL de Documentos (<c>documentos/Documento/{id}/conteudo</c>).</para></summary>
public sealed record AgendamentoResumoDto(
    Guid Id,
    DateTime? InicioEm,
    DateTime? FimEm,
    string Tipo,
    string Titulo,
    string? Profissional,
    string? Unidade,
    string Status,
    Guid? SolicitacaoExameId = null,
    string? StatusConfirmacao = null,
    bool PodeResponder = false,
    string? Origem = null,
    bool NaFila = false,
    string Momento = "Proximo",
    bool TemHora = true,
    Guid? GuiaDocumentoId = null);
