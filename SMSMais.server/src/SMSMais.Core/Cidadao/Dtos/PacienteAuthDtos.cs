namespace SMSMais.Core.Cidadao.Dtos;

public sealed record SolicitarOtpRequest(string Cpf);

/// <summary>
/// O que o app deve fazer depois de informar o CPF (passo 1 do login).
/// </summary>
public static class SituacaoLoginCidadao
{
    /// <summary>Contato verificado: o código já foi enviado para o número verificado.</summary>
    public const string Otp = "otp";

    /// <summary>
    /// Cadastro existe mas o contato NÃO é verificado: o cidadão precisa provar quem é
    /// (nascimento + nº da solicitação SISREG) e informar o telefone que vai receber o código.
    /// </summary>
    public const string Verificacao = "verificacao";

    /// <summary>
    /// CPF sem cadastro na Saúde: o cidadão informa nascimento + telefone; o par CPF/nascimento
    /// é conferido na Receita (proxy CPF) e o cadastro é criado quando o código é confirmado.
    /// </summary>
    public const string Cadastro = "cadastro";
}

/// <summary>
/// Resultado de solicitar o código. No fluxo normal o código vai só pelo WhatsApp e
/// <see cref="CodigoTeste"/> é null. Ele só é preenchido como <b>fallback</b> quando o
/// WhatsApp não está configurado no servidor (ou <c>Tfd:Otp:ModoTeste=true</c>), para não
/// travar o login — aí o código é exibido na tela. <see cref="TelefoneMascarado"/> traz uma
/// dica do destino (ex.: <c>***-1234</c>) para o usuário conferir.
/// <see cref="Situacao"/> (ver <see cref="SituacaoLoginCidadao"/>) diz ao app se o código saiu
/// (<c>otp</c>) ou se ainda faltam dados (<c>verificacao</c> | <c>cadastro</c>) — nesses dois
/// casos nada foi enviado e <see cref="Enviado"/> é <c>false</c>.
/// </summary>
public sealed record OtpEmitidoDto(
    bool Enviado,
    string Canal,
    string? CodigoTeste,
    int ValidadeSegundos,
    string? TelefoneMascarado = null,
    string Situacao = SituacaoLoginCidadao.Otp);

/// <summary>
/// Passo 2 do login de quem NÃO tem contato verificado (ou perdeu o número): prova de identidade
/// + o telefone que vai receber o código. <see cref="CodigoSolicitacao"/> é o nº da solicitação
/// do SISREG e é <b>obrigatório</b> quando já existe cadastro; é ignorado no cadastro novo
/// (não há solicitação nossa para conferir — quem valida é a Receita).
/// </summary>
public sealed record SolicitarOtpVerificacaoRequest(
    string Cpf, DateOnly DataNascimento, string? CodigoSolicitacao, string Telefone);

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
/// <para><c>TemHora</c> = false: a fonte só deu o dia — a tela não inventa "00:00".</para></summary>
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
    bool TemHora = true);
