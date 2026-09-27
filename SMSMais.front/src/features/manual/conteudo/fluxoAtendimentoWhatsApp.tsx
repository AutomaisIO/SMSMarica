import type { ReactNode } from 'react';
import { Workflow } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { AbaRef, BotaoRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo CONCEITUAL (pedido do dono em 27/09/2026): a lógica por trás do atendimento automático
 * do WhatsApp, do agendamento novo até o robô — didático e VISUAL, porque a equipe vive as pontas
 * (Confirmações, Conversas, Mensageria) sem enxergar a engrenagem que liga uma à outra.
 *
 * Não documenta uma tela (sem `rota`): documenta as DECISÕES. Quando uma regra daqui mudar no
 * código, este artigo muda no mesmo commit — como qualquer outro.
 */

// ---------- peças visuais do mapa (só deste artigo) ----------

function Caixa({ tom, titulo, children }: { tom: 'sistema' | 'pessoa' | 'decisao' | 'fim'; titulo: string; children?: ReactNode }) {
  const classes =
    tom === 'pessoa'
      ? 'border-sky-300 bg-sky-50'
      : tom === 'decisao'
        ? 'border-amber-300 bg-amber-50'
        : tom === 'fim'
          ? 'border-emerald-300 bg-emerald-50'
          : 'border-gray-300 bg-white';
  return (
    <div className={`rounded-lg border px-3 py-2 text-xs shadow-sm ${classes}`}>
      <p className="font-semibold text-gray-900">{titulo}</p>
      {children && <div className="mt-0.5 text-gray-600">{children}</div>}
    </div>
  );
}

function Seta({ rotulo }: { rotulo?: string }) {
  return (
    <div className="flex items-center gap-2 py-0.5 pl-6 text-[11px] text-gray-500">
      <span aria-hidden>↓</span>
      {rotulo && <span className="italic">{rotulo}</span>}
    </div>
  );
}

function Ramo({ children }: { children: ReactNode }) {
  return <div className="ml-6 border-l-2 border-dashed border-gray-200 pl-3">{children}</div>;
}

// ---------- o artigo ----------

export const artigoFluxoAtendimentoWhatsApp: Artigo = {
  slug: 'fluxo-atendimento-whatsapp',
  titulo: 'Como o WhatsApp decide: a lógica do atendimento',
  resumo:
    'O mapa das decisões por trás das mensagens automáticas: por que a primeira mensagem não leva os dados, como a identificação funciona, quando o robô entra e quando ele cala.',
  grupo: 'atendimento',
  icone: Workflow,
  publico: 'Toda a equipe que atende pelo WhatsApp — e quem precisa explicar o sistema para alguém',
  atualizadoEm: '2026-09-27',
  palavrasChave: [
    'fluxo',
    'lógica',
    'decisão',
    'como funciona',
    'por trás',
    'robô',
    'atendente virtual',
    'ia',
    'identificação',
    'desafio',
    '4 dígitos do cpf',
    'mês e ano de nascimento',
    'primeira mensagem',
    'por que não vem a data',
    'janela de 24 horas',
    'retomar com o robô',
    'retomada',
    'lembrete',
    'reforço',
    'orientação ao posto',
    'quero mais informações',
    'não sou essa pessoa',
    'vou ao posto',
    'sim confirmo',
    'não poderei ir',
    'cancelar',
    'trava',
    'silêncio',
    'quando o robô responde',
    'quando o robô cala',
  ],
  secoes: () => [
    {
      id: 'mapa',
      titulo: 'O mapa em uma tela',
      busca: 'mapa visual diagrama fluxo completo resumo geral',
      conteudo: (
        <div className="space-y-4">
          <P>
            Tudo que o cidadão recebe no WhatsApp nasce deste fluxo. Caixas <strong>brancas</strong> são o
            sistema agindo; <strong>azuis</strong>, a pessoa; <strong>amarelas</strong>, uma decisão;{' '}
            <strong>verdes</strong>, um desfecho.
          </P>
          <div className="max-w-md space-y-0.5">
            <Caixa tom="sistema" titulo="Agendamento novo (SISREG)">
              importado ou criado — entra na fila de comunicações
            </Caixa>
            <Seta rotulo="o número do cadastro é verificado?" />
            <Caixa tom="decisao" titulo="Número verificado?" />
            <Ramo>
              <Seta rotulo="SIM" />
              <Caixa tom="fim" titulo="Mensagem completa">
                procedimento, data, local, link do app e botões de presença
              </Caixa>
            </Ramo>
            <Ramo>
              <Seta rotulo="NÃO" />
              <Caixa tom="sistema" titulo="Primeira mensagem curta (sem dados)">
                “Seu exame foi agendado!” + botões — os dados vêm depois da identificação
              </Caixa>
              <Seta rotulo="a pessoa toca em…" />
              <Caixa tom="pessoa" titulo="“Quero mais informações”" />
              <Seta />
              <Caixa tom="sistema" titulo="Identificação (a máquina, sem IA)">
                4 primeiros dígitos do CPF → mês e ano de nascimento → confirma o nome → vínculo
              </Caixa>
              <Seta rotulo="conferiu (até 3 chances)" />
              <Caixa tom="fim" titulo="Dados na própria conversa, na hora">
                procedimento, data, local, guia, link — e “Você confirma a presença?” com botões
              </Caixa>
            </Ramo>
            <Seta rotulo="se a pessoa não responde nada…" />
            <Caixa tom="sistema" titulo="Insistência com regras (Mensageria → Regras)">
              lembrete das vésperas · reforço (3 dias) · orientação ao posto (terminal)
            </Caixa>
            <Seta rotulo="e o texto livre (dúvida, pergunta, desabafo)?" />
            <Caixa tom="decisao" titulo="Tem gente cuidando?">
              humano na janela cala o automático; senão, o robô (IA) responde — com limites
            </Caixa>
          </div>
          <Callout tipo="dica" titulo="Uma frase para guardar">
            <strong>Máquina primeiro, IA depois, gente sempre por cima.</strong> Botões, dígitos e datas
            são da máquina determinística (barata e auditável); conversa solta é do robô; e qualquer
            atendente que entra faz o automático recuar.
          </Callout>
        </div>
      ),
    },
    {
      id: 'por-que-curta',
      titulo: 'Por que a primeira mensagem não leva a data',
      busca: 'lgpd primeira mensagem curta sem dados número de família destinatário correto',
      conteudo: (
        <div className="space-y-4">
          <P>
            O telefone do cadastro <strong>não prova quem está do outro lado</strong>: número muda de dono,
            é o celular da filha, foi digitado errado na recepção. Nos dados reais, um mesmo número atende
            até 5 pessoas da mesma família — e às vezes atende um estranho.
          </P>
          <P>
            Dado de saúde (que exame, onde, quando) só sai para <strong>destinatário provado</strong>{' '}
            (LGPD). Por isso a primeira mensagem para número não verificado apenas avisa que{' '}
            <em>existe</em> um agendamento e convida a pessoa a se identificar. Quem já provou o número uma
            vez não passa por isso de novo: recebe tudo de primeira.
          </P>
          <Callout tipo="lgpd" titulo="O que nunca sai antes da identificação">
            Procedimento, data, hora, local e qualquer link que abra o app. O robô também é proibido de
            confirmar ou negar o que houver no cadastro — mesmo que a pessoa pergunte diretamente.
          </Callout>
        </div>
      ),
    },
    {
      id: 'identificacao',
      titulo: 'A identificação, passo a passo',
      busca: 'interrogatório chances erros reorientação recomeço tentar de novo esgotado posto',
      conteudo: (
        <div className="space-y-4">
          <P>
            A identificação é uma <strong>máquina de estados determinística</strong> — não é a IA. Cada
            resposta válida avança uma etapa; cada dado errado gasta uma das <strong>3 chances</strong>:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: '1 · Início do CPF', descricao: 'Os 4 primeiros dígitos do CPF DO PACIENTE (não de quem digita). Mandar o CPF inteiro também vale — o sistema usa só o começo.' },
              { termo: '2 · Mês e ano de nascimento', descricao: 'Formatos tolerantes ("07/1949", "julho de 1949"). Só a data completa errada gasta chance; texto não entendido só re-orienta.' },
              { termo: '3 · Nome', descricao: 'O sistema mostra o nome completo do candidato e pede um Sim/Não — é a trava contra homônimos de CPF parecido.' },
              { termo: '4 · Vínculo', descricao: '"Sou o paciente / Sou responsável / Outro parente" — fica gravado no cadastro: é a resposta de LGPD para "por que essa pessoa recebe o dado daquela".' },
            ]}
          />
          <Sub>O que acontece quando a conversa engasga</Sub>
          <Lista>
            <Item>
              <strong>Tocou de novo em “Quero mais informações” no meio do diálogo?</strong> O sistema
              repete a pergunta da etapa (“só falta o mês e o ano…”), sem jogar a pessoa de volta ao início.
            </Item>
            <Item>
              <strong>Parou por mais de um dia e voltou?</strong> Recomeça pelo CPF — mas as chances gastas
              não voltam.
            </Item>
            <Item>
              <strong>3 mensagens seguidas não entendidas?</strong> Em vez de silêncio, a pessoa recebe a
              passagem para a equipe com um botão <BotaoRef>Tentar de novo</BotaoRef> — e dígitos válidos
              continuam sendo aceitos a qualquer momento.
            </Item>
            <Item>
              <strong>Esgotou as 3 chances?</strong> O caminho vira o posto de saúde (com documento) — e um
              toque em botão não devolve as chances, senão seria tentativa infinita no dado do paciente.
            </Item>
          </Lista>
        </div>
      ),
    },
    {
      id: 'depois-da-identificacao',
      titulo: 'Identificou: o que sai — e a frase diz a verdade',
      busca: 'cadastro confirmado chegam em instantes na conversa dados na hora frase honesta outro número verificado aviso',
      conteudo: (
        <div className="space-y-4">
          <P>
            Ao concluir, o número fica <strong>verificado para aquele paciente</strong> e o que estava
            retido sai <strong>na própria conversa, na hora</strong> — uma mensagem com procedimento, data,
            local, orientação da guia, link do app e a pergunta de presença com botões{' '}
            <BotaoRef>Sim, confirmo</BotaoRef> / <BotaoRef>Não poderei ir</BotaoRef>. Sem fila, sem esperar
            worker.
          </P>
          <P>
            E a frase final é calculada pelo que <em>de fato</em> aconteceu — nunca mais “chegam em
            instantes” com nada atrás:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Liberou', descricao: '"Já estou enviando as informações…" — e elas chegam na sequência, na mesma conversa.' },
              { termo: 'Uma atendente já assumiu', descricao: 'Diz que a equipe já cuida do agendamento. É promessa: quem está com a ficha precisa falar com a pessoa.' },
              { termo: 'Já passou / cancelado', descricao: 'Diz isso com a data e orienta o posto para remarcar.' },
              { termo: 'Já enviado / nada pendente', descricao: 'Aponta a mensagem anterior com os dados — e só conta como "enviado" o envio que levou dados de verdade.' },
              { termo: 'Outro número já verificado', descricao: 'A pessoa venceu o desafio completo: recebe os dados AQUI. O número principal do cadastro não muda sozinho — e recebe um aviso de segurança (família compartilha aparelho; quem tem o número principal precisa saber).' },
            ]}
          />
        </div>
      ),
    },
    {
      id: 'botoes',
      titulo: 'Cada botão, cada consequência',
      busca: 'botões quero mais informações não sou essa pessoa vou ao posto confirmar cancelar duas fases motivo',
      conteudo: (
        <div className="space-y-4">
          <ListaDefinicoes
            itens={[
              { termo: 'Quero mais informações', descricao: 'Abre a identificação — e, se o número JÁ é o verificado do paciente, pula tudo e entrega os dados na hora. Sem aviso pendente, explica em vez de calar.' },
              { termo: 'Não sou essa pessoa', descricao: 'Pergunta "você conhece Fulano?". "Não conheço" marca o número como inválido para aquele paciente: nada automático sai mais para ele até a recepção corrigir.' },
              { termo: 'Vou ao posto (na orientação)', descricao: 'Encerra os automáticos daquele agendamento — a pessoa resolve presencialmente e ninguém mais insiste.' },
              { termo: 'Sim, confirmo', descricao: 'Confirma a presença na hora (vale também para quem tinha avisado que não ia e mudou de ideia).' },
              { termo: 'Não poderei ir', descricao: 'Pergunta em duas fases ("Quer CANCELAR?") para proteger o toque acidental — e vale MESMO para quem já tinha confirmado: confirmou pelo link e a vida mudou, cancela do mesmo jeito. O cancelamento é local e cai na aba Cancelamento; o SISREG segue com a equipe. O motivo é opcional.' },
            ]}
          />
          <Callout tipo="atencao" titulo="Nunca silêncio">
            Toque de botão sem resposta era o pior defeito do sistema (o robô descarta cliques, então
            ninguém respondia). Hoje todo clique termina em resposta: a certa, ou uma explicação honesta.
          </Callout>
        </div>
      ),
    },
    {
      id: 'insistencia',
      titulo: 'A insistência tem régua (e interruptor)',
      busca: 'lembrete reforço orientação ao posto régua limites 48 horas domingo chaves desligadas',
      conteudo: (
        <div className="space-y-4">
          <P>
            Quem não responde não é bombardeado. A insistência segue uma régua com{' '}
            <strong>chaves em Mensageria → Regras</strong> (o reforço e a orientação nascem desligados) e{' '}
            <strong>limites por número</strong>: 48h entre automáticos, no máximo um reforço a cada 7 dias e
            uma orientação a cada 20, nunca no domingo, sempre dentro da janela de horário.
          </P>
          <Lista>
            <Item><strong>Lembrete das vésperas</strong> — quem já confirmou recebe com os dados; quem nunca respondeu recebe o convite a se identificar de novo.</Item>
            <Item><strong>Reforço</strong> — “nossa mensagem chegou e a resposta não”, sem dado nenhum.</Item>
            <Item><strong>Orientação ao posto</strong> — a última: “não vamos mais insistir; a guia está no posto”. Depois dela, silêncio de propósito.</Item>
          </Lista>
          <P>
            E <strong>uma atendente pegar a ficha desliga tudo isso</strong> para aquele agendamento — o
            automático não fala por cima de gente.
          </P>
        </div>
      ),
    },
    {
      id: 'robo-ia',
      titulo: 'Quando o robô (IA) entra — e quando ele cala',
      busca: 'robô ia atendente virtual entra cala trava humano expediente parar robô teto interações retomada resumo',
      conteudo: (
        <div className="space-y-4">
          <P>
            O robô de IA só pega o que a máquina não resolve: <strong>texto livre</strong> — dúvida,
            desconfiança, “como chego lá?”. E mesmo aí ele obedece uma hierarquia de travas:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Humano na janela', descricao: 'Uma atendente escreveu nesta janela de 24h? O robô cala e não volta — a não ser que ela devolva a conversa ao "Atendente Virtual".' },
              { termo: 'Parar robô', descricao: 'Bloqueio forte por conversa, na mão da atendente. Vence tudo, inclusive a virada do dia.' },
              { termo: 'Identificação em andamento', descricao: 'Com o diálogo determinístico vivo no número, o robô não atravessa — quem conduz é a máquina.' },
              { termo: 'Teto de interações', descricao: 'Bateu o limite do assunto, o robô avisa UMA vez que a equipe assume — e para de responder.' },
              { termo: 'Guardrails', descricao: 'Nunca revela agendamento antes da identidade, nunca pede CPF completo, nunca promete ação que não executou com ferramenta, nunca inventa canais que não existem.' },
            ]}
          />
          <Sub>Retomada pedida pela equipe</Sub>
          <P>
            Conversa que morreu sem resposta (erro antigo, atendente que não voltou)? O botão{' '}
            <BotaoRef>Retomar c/ robô</BotaoRef> na conversa manda a IA fazer um <strong>resumo do ponto em
            que parou</strong> e perguntar como a pessoa quer seguir — aproveitando a janela de 24h enquanto
            está aberta. A conversa não muda de dono, e as travas acima continuam valendo.
          </P>
          <P>
            A supervisão tem a versão <strong>em lote</strong>: o botão <BotaoRef>Retomar largadas</BotaoRef>{' '}
            no topo da Central conta quantas conversas estão com a última palavra do cidadão e a janela
            aberta, mostra a prévia do lote (quem terminou só em “obrigado/ok” fica de fora) e dispara as
            retomadas de uma vez — o robô responde uma a uma pela fila normal.
          </P>
        </div>
      ),
    },
    {
      id: 'quem-manda',
      titulo: 'Quem manda em quem (a hierarquia)',
      busca: 'hierarquia prioridade máquina ia humano posse substituída por atendente',
      conteudo: (
        <div className="space-y-4">
          <Lista>
            <Item><strong>1º A máquina determinística</strong> — botões, dígitos, datas, Sim/Não. Barata, auditável, 1 segundo. É ela que guarda a porta da identidade.</Item>
            <Item><strong>2º O robô de IA</strong> — linguagem solta, sob as travas da seção anterior. Tudo que ele executa passa por ferramentas tipadas com trilha (nada de SQL livre).</Item>
            <Item><strong>3º (e acima de todos) a pessoa da equipe</strong> — assumiu a ficha ou a conversa, o automático recua e só volta por ordem explícita (<AbaRef>devolver ao robô</AbaRef>, retomada).</Item>
          </Lista>
          <Callout tipo="dica" titulo="Se algo parecer errado">
            Toda mensagem automática fica na conversa do paciente e na Mensageria (com motivo quando não
            saiu). Resposta estranha do robô tem o “Marcar erro” na bolha — vira treino, não bronca.
          </Callout>
        </div>
      ),
    },
  ],
};
