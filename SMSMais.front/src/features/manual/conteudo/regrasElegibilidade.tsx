import { BookOpen } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { BotaoRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo da tela Regulação → Regras de elegibilidade (plano 03): a curadoria das regras do manual
 * da regulação por procedimento, e a prévia "Ver como o solicitante vê".
 *
 * Conferido no código em 09/10/2026: `features/regulacao` (RegrasElegibilidadePage,
 * FormularioRegra, ListaProcedimentosRegrados, PreviaSolicitante, wizard/VisaoRegras) e no backend
 * (`RegulacaoRegraService` — criar nasce ativa, nova versão desativa a anterior, `PreviaAsync` só
 * leitura —, `AvaliadorElegibilidade` — tudo soma com E —, `PendenciasDasRegras` e
 * `ModuloPermissao.RegulacaoConfiguracao`).
 */
export const artigoRegrasElegibilidade: Artigo = {
  slug: 'regras-elegibilidade',
  titulo: 'Regras de elegibilidade',
  resumo:
    'Onde se cadastra o que o manual da regulação exige por procedimento — e onde se confere, pela prévia, o que o solicitante vai ver no passo Regras.',
  grupo: 'regulacao',
  icone: BookOpen,
  rota: '/app/regulacao/regras',
  publico: 'Quem cuida das regras da regulação (módulo Regulação — Configuração)',
  atualizadoEm: '2026-10-09',
  palavrasChave: [
    'regras',
    'regras de elegibilidade',
    'elegibilidade',
    'manual',
    'manual da regulação',
    'CRECE',
    'REUNI',
    'curadoria',
    'nova regra',
    'ativa',
    'inativa',
    'excluir regra',
    'pergunta',
    'lista',
    'basta um',
    'documento',
    'idade',
    'sexo',
    'CID',
    'informativa',
    'ver como o solicitante vê',
    'prévia',
    'conferir regras',
    'local da lesão',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve esta tela',
      busca: 'regras manual procedimento passo regras nova solicitação análise automática ser sernit esus técnico regulador conferir',
      conteudo: (
        <>
          <P>
            Aqui fica, por procedimento, o que o manual da regulação exige: as perguntas sobre o paciente,
            os documentos obrigatórios, as faixas de idade. Essas regras aparecem para a unidade no passo{' '}
            <strong>Regras</strong> da Nova solicitação e são as mesmas que a análise automática aplica aos
            pedidos que chegam do SER, do SERNIT e do ESUS de São Gonçalo.
          </P>
          <P>
            Regra bem cadastrada é o que poupa o técnico regulador de conferir o manual de novo: o pedido só
            sai da unidade com as perguntas respondidas e os documentos anexados.
          </P>
          <P>
            Escolha o procedimento na busca — ou na lista dos mais pedidos, que aparece quando a busca está
            vazia — para ver e mexer nas regras dele.
          </P>
        </>
      ),
    },
    {
      id: 'tipos',
      titulo: 'Os quatro tipos de regra',
      busca: 'tipo o sistema decide pergunta ao solicitante exige documento só informa idade sexo cid lista basta um nenhuma destas soma',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              {
                termo: 'O sistema decide',
                descricao:
                  'Idade mínima ou máxima, sexo, CPF ou CID. O sistema confere pelo cadastro do paciente, sem perguntar nada.',
              },
              {
                termo: 'Pergunta ao solicitante',
                descricao:
                  'Sim, Não ou Não sei — ou uma lista de condições em que basta marcar uma. Você diz qual resposta barra.',
              },
              {
                termo: 'Exige documento',
                descricao:
                  'Vira caixinha de anexo no pedido. Obrigatória ou opcional (o "se houver" do manual é opcional).',
              },
              {
                termo: 'Só informa',
                descricao: 'Texto do manual que aparece para o solicitante e não barra nada.',
              },
            ]}
          />
          <Callout tipo="regra" titulo="Todas as regras ativas valem juntas">
            O pedido precisa atender a todas as regras ativas do procedimento ao mesmo tempo. Por isso,
            critério alternativo do manual (“portadores das seguintes condições: …”) se cadastra como{' '}
            <strong>uma pergunta de lista</strong>, e não como várias perguntas soltas — e faixa de idade
            alternativa vira pergunta, não regra de idade.
          </Callout>
          <P>
            Quando o requisito muda conforme algo que o solicitante escolhe — o <em>local da lesão</em> na
            Oncologia, a indicação na Ortopedia —, ainda não há regra que só valha para uma escolha. O
            caminho é uma pergunta de lista em que cada opção traz o requisito daquele local, escrito no
            próprio texto da opção.
          </P>
        </>
      ),
    },
    {
      id: 'cadastrar',
      titulo: 'Cadastrar, ligar, desligar',
      busca: 'nova regra ativa inativa excluir mostrar inativas corrigir regra versão importar csv',
      conteudo: (
        <>
          <Lista>
            <Item>
              <BotaoRef>Nova regra</BotaoRef> — o que é cadastrado à mão já nasce <strong>ativo</strong> e passa
              a valer no próximo pedido.
            </Item>
            <Item>
              O botão <BotaoRef>Ativa</BotaoRef> / <BotaoRef>Inativa</BotaoRef> de cada regra liga e desliga.
              Regra inativa não aparece para ninguém; marque <strong>mostrar inativas</strong> para vê-las.
            </Item>
            <Item>
              Para corrigir uma regra, cadastre a certa e deixe a antiga <strong>Inativa</strong>. Evite
              excluir: a resposta que um pedido antigo deu continua apontando para a regra que a pessoa viu.
            </Item>
          </Lista>
          <Callout tipo="atencao" titulo="Não reimporte o CSV do manual">
            A importação dos manuais já foi feita e revisada. O arquivo antigo tem erros conhecidos de
            extração (tabelas inteiras lidas errado); importar de novo traria regras falsas de volta.
          </Callout>
        </>
      ),
    },
    {
      id: 'previa',
      titulo: 'Ver como o solicitante vê',
      busca: 'ver como o solicitante vê prévia conferir destino idade sexo passo regras simular nada gravado',
      conteudo: (
        <>
          <P>
            Com o procedimento escolhido, <BotaoRef>Ver como o solicitante vê</BotaoRef> abre o passo{' '}
            <strong>Regras</strong> exatamente como a unidade vai encontrá-lo — as mesmas perguntas, caixinhas,
            avisos e bloqueios. É o jeito de conferir uma regra sem abrir uma solicitação de verdade.
          </P>
          <Lista>
            <Item>
              <strong>Destino</strong>: as regras podem ser só do SER, só do SERNIT…; a prévia mostra o que vale
              para o destino escolhido.
            </Item>
            <Item>
              <strong>Idade e sexo</strong>: opcionais. Preencha para ver a regra que o sistema decide sozinho
              barrar (ex.: idade mínima). Em branco, essa regra fica “a conferir”, como fica para o paciente
              sem data de nascimento.
            </Item>
            <Item>Só as regras <strong>ativas</strong> aparecem. Ligou ou desligou uma regra, a prévia acompanha.</Item>
            <Item>Nada ali é gravado, e os botões de resposta ficam desligados.</Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'o-que-trava',
      titulo: 'O que segura o envio do pedido',
      busca: 'trava envio pré-regulação pergunta sem resposta documento obrigatório destino barrado não sei não trava ressalva',
      conteudo: (
        <>
          <P>
            Na Nova solicitação, o <BotaoRef>Enviar para a pré-regulação</BotaoRef> só libera quando:
          </P>
          <Lista>
            <Item>toda pergunta que barra foi respondida (Sim, Não ou Não sei);</Item>
            <Item>todo documento obrigatório tem anexo;</Item>
            <Item>as regras não barraram o destino escolhido.</Item>
          </Lista>
          <P>
            <strong>“Não sei” não trava</strong>: o pedido segue e o regulador confere. Pergunta cadastrada com
            severidade de ressalva ou aviso também não trava. Regra de outro sistema (uma pergunta só do SER)
            não segura pedido que vai para o SISREG.
          </P>
        </>
      ),
    },
  ],
};
