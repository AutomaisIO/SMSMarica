namespace SMSMais.Core.Regulacao.FollowUp;

/// <summary>
/// As regras de follow-up medidas no spike d sobre <b>18.904 eventos reais</b> do SER e do SERNIT
/// (<c>SMSMais.Regulacao/revisoes/spike-d-followup.md</c>).
///
/// <para>Ficam em código, e não em migration nem em seed de banco: migration e imutavel e roda
/// igual em toda instancia nova (CLAUDE.md, regra 9), e cada municipio pode querer as suas. A
/// configuracao nasce <b>vazia</b> — classificador desligado —, e a tela de configuracao oferece
/// esta semente como ponto de partida, para o configurador ajustar em cima de algo medido em vez
/// de escrever regex de 600 caracteres do zero.</para>
///
/// <para><b>Sao nove categorias, nao as quatro que o plano supunha.</b> As duas maiores
/// (<c>SemVaga</c>, 22% do SER, e <c>ReclassificacaoRisco</c>, 81% do SERNIT) nao estavam
/// previstas, e o que parecia "documento criticado" era na verdade orientacao ao paciente, que
/// nao pede acao nenhuma da unidade. Só <c>FalhaContato</c> e <c>SolicitacaoAoSolicitante</c>
/// viram pendencia.</para>
/// </summary>
public static class SementeFollowUp
{
    /// <summary>Versao da semente, para o dia em que ela mudar e alguem precisar saber qual usou.</summary>
    public const int Versao = 1;

    /// <summary>JSON pronto para ir direto ao campo <c>regras_followup_json</c>.</summary>
    public const string Json =
        """
        [
          {
            "categoria": "ReclassificacaoRisco",
            "ordem": 1,
            "padrao": "RISCO RECLASSIFICADO|RECLASSIFICAD[OA] PELO REGULADOR",
            "vira_pendencia": null
          },
          {
            "categoria": "FalhaContato",
            "ordem": 2,
            "padrao": "SEM CONTATO|N[AO]O (?:FOI POSSIVEL|OBTIVEMOS|CONSEGUIMOS)\\s+(?:EFETUAR\\s+)?(?:O\\s+)?(?:CONTATO|SUCESSO|EXITO)|(?:TENTAMOS|REALIZAD[AO]S?|FEIT[AO]S?)\\s+(?:DIVERSAS?|VARI[AO]S?|MUIT[AO]S?|ALGUM[AO]S?|\\d+)\\s+(?:TENTATIVAS?|CONTATOS?)|TENTATIVAS? DE CONTATO|TELEFONE N[AO]O COMPLETA|N[AO]O COMPLETA A CHAMADA|(?:TELEFONE|NUMERO)S?\\s+(?:INEXISTENTE|INVALIDO|DESLIGADO|ERRAD[OA]|N[AO]O EXISTE|FORA DE AREA)|(?:TELEFONE|NUMERO|CONTATO)S?.{0,30}(?:E DE OUTRA PESSOA|N[AO]O E D[AO] PACIENTE|SAO ENGANO|E ENGANO|INFORMAM SER ENGANO)|CAIXA POSTAL|N[AO]O ATENDE(?:U|RAM)?\\b|SEM SUCESSO|SEM EXITO|VARIAS TENTATIVAS",
            "vira_pendencia": "contato"
          },
          {
            "categoria": "ContatoRealizado",
            "ordem": 3,
            "padrao": "CONTATO REALIZADO|CONTATO EFETUADO|APOS CONTATO|EM CONTATO (?:COM|TELEFONIC|POR)|PACIENTE INFORMOU|FOMOS INFORMAD|A MESMA INFORMOU|O MESMO INFORMOU|CIENTE (?:DO AGENDAMENTO|DA CONSULTA|DO EXAME|DA DATA)|FALEI COM (?:O|A) PACIENTE|USUARI[OA] (?:FOI )?INFORMAD|DADA CIENCIA|DEU CIENCIA|CIENCIA AO PACIENTE|INFORMARAM QUE",
            "vira_pendencia": null
          },
          {
            "categoria": "CancelamentoOuReagendamento",
            "ordem": 4,
            "padrao": "FAVOR CANCELAR|SOLICITO (?:O )?(?:CANCELAMENTO|REAGENDAMENTO)|PEDIDO DE CANCELAMENTO|(?:SER |SE )?REAGENDAD[OA]|POSSIBILIDADE DE SER REAGENDAD|CANCELAR (?:O |A )?(?:AGENDAMENTO|CONSULTA|PACIENTE)",
            "vira_pendencia": null
          },
          {
            "categoria": "SolicitacaoAoSolicitante",
            "ordem": 5,
            "padrao": "FAVOR (?:INFORMAR|ENVIAR|ANEXAR|INSERIR|ATUALIZAR)(?!\\s+(?:AO|A|O)\\s+PACIENTE)|SOLICIT\\w*\\s+(?:O\\s+|A\\s+)?(?:ENVIO|INFORMAR|ATUALIZACAO)|INFORMAR (?:PESO|ALTURA|IMC|CID|TELEFONE|CONTATO ATUAL|LAUDO|EXAME|SE )|(?:ANEXAR|INSERIR|ENVIAR)\\s+(?:O\\s+|A\\s+)?(?:LAUDO|EXAME|RELATORIO|ENCAMINHAMENTO|GUIA|DOCUMENT)|DOCUMENTA[CS][AO]O (?:PENDENTE|INCOMPLETA|ILEGIVEL|INSUFICIENTE|DESATUALIZADA)|(?:LAUDO|EXAME|RELATORIO)\\s+(?:ILEGIVEL|INCOMPLETO|DESATUALIZADO|VENCIDO|AUSENTE)|PENDENCIA DE DOCUMENT|(?:NECESSARIO|PRECISA) O ENVIO|ENVIO DA CHAVE",
            "vira_pendencia": "documento"
          },
          {
            "categoria": "OrientacaoAoPaciente",
            "ordem": 6,
            "padrao": "(?:FAVOR |)INFORMAR AO PACIENTE|ORIENTAR O PACIENTE|COMUNICAR AO PACIENTE|INFORMAR O PACIENTE|COMUNICAR O\\(?A\\)? PACIENTE|DOCUMENTOS DE IDENTIFICACAO PESSOAL",
            "vira_pendencia": null
          },
          {
            "categoria": "SemVaga",
            "ordem": 7,
            "padrao": "SEM (?:AGENDA|VAGA)|N[AO]O H[AA] (?:AGENDA|VAGA)|NENHUMA AGENDA|SEM AGENDAMENTO|AGUARD\\w*(?:\\s+\\w+){0,3}?\\s+(?:VAGA|AGENDA|AGENDAMENTO)|APT[OA]\\W*(?:\\(A\\))?\\W*(?:AGUARD|SEM|A REGULACAO)|EM FILA|FILA DE ESPERA|DISPONIBILIDADE DE AGENDA|SEM DISPONIBILIDADE|SEM PRESTADOR|\\bAG\\.?\\s+(?:VAGA|AGENDA)",
            "vira_pendencia": null
          },
          {
            "categoria": "Agendamento",
            "ordem": 8,
            "padrao": "AGENDAD[OA] PARA|AGENDAMENTO (?:REALIZADO|CONFIRMADO|EFETUADO|ADMINISTRATIVO)|CONSULTA MARCADA|VAGA (?:DISPONIBILIZADA|OFERTADA|EXTRA)",
            "vira_pendencia": null
          }
        ]
        """;
}
