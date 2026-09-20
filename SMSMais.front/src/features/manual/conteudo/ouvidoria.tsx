import { Megaphone } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { AbaRef, BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import { Passos } from '@/features/manual/components/Passos';
import { FluxoManifestacao } from '@/features/manual/simulacoes/FluxoManifestacao';
import type { Artigo } from '@/features/manual/tipos';
import { instituicao } from '@/shared/tema/instituicao';

/**
 * Artigo da Ouvidoria (ADR-0060). A ordem segue a dúvida de quem chega: o que é isso e o que a
 * ouvidoria não faz → os seis tipos → quem manifesta e quem pode saber → protocolo e relógios →
 * a fila → registrar → tramitar → responder e concluir → os casos especiais (denúncia, ponto de
 * resposta, gestão) → quem pode o quê → dúvidas.
 *
 * Tudo o que está escrito aqui foi conferido em `features/ouvidoria/lib/regras.ts`, nas páginas e
 * em `perfis/lib/acoes.ts`. Os prazos são os PADRÕES da configuração — a tela Configuração pode
 * mudá-los, e o texto diz isso onde importa.
 */

/** Tabela pequena de permissões: módulo × ação. Sem componente compartilhado para não inventar um. */
function TabelaPermissoes({ linhas }: { linhas: { modulo: string; acoes: [string, string, string, string] }[] }) {
  const cab = ['Consulta', 'Inclusão', 'Edição', 'Exclusão'];
  return (
    <div className="max-w-3xl overflow-x-auto rounded-theme-md border border-gray-200 bg-white">
      <table className="w-full text-left text-sm">
        <thead className="bg-gray-50 text-xs uppercase tracking-wide text-gray-500">
          <tr>
            <th className="px-3 py-2 font-medium">Módulo do perfil</th>
            {cab.map((c) => (
              <th key={c} className="px-3 py-2 font-medium">
                {c}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100 align-top">
          {linhas.map((l) => (
            <tr key={l.modulo}>
              <td className="px-3 py-2 font-medium text-gray-900">{l.modulo}</td>
              {l.acoes.map((a, i) => (
                <td key={i} className={a === '—' ? 'px-3 py-2 text-gray-400' : 'px-3 py-2 text-gray-600'}>
                  {a}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export const artigoOuvidoria: Artigo = {
  slug: 'ouvidoria',
  titulo: 'Ouvidoria',
  resumo:
    'Receber, registrar, encaminhar, cobrar e responder as manifestações do cidadão sobre a saúde — com protocolo, prazo legal e sigilo de quem manifesta.',
  grupo: 'atendimento',
  icone: Megaphone,
  rota: '/app/ouvidoria',
  publico: 'Quem atende a ouvidoria, quem responde por uma unidade ou área e quem gere o serviço',
  atualizadoEm: '2026-09-20',
  palavrasChave: [
    'ouvidoria',
    'manifestação',
    'reclamação',
    'denúncia',
    'elogio',
    'sugestão',
    'solicitação',
    'informação',
    'protocolo',
    'código de acesso',
    'prazo',
    'prorrogação',
    '30 dias',
    'ponto de resposta',
    'sigilo',
    'sigilosa',
    'anônimo',
    'anônima',
    'complementação',
    'recurso',
    'arquivar',
    'arquivamento',
    'triagem',
    'encaminhar',
    'cobrar',
    'escalonar',
    'apuração',
    'pseudonimizado',
    'habilitar denúncia',
    'resolutividade',
    'situação final',
    'Lei 13.460',
    'OuvidorSUS',
    'Fala.BR',
    'manifestante',
    'meu ponto',
    'assuntos',
    'marcadores',
    'painel',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve esta tela',
      busca: 'o que é ouvidoria de onde vem a manifestação canal presencial telefone whatsapp o que a ouvidoria não faz não investiga não marca consulta',
      conteudo: (
        <div className="space-y-4">
          <P>
            A ouvidoria é a porta por onde o cidadão diz o que aconteceu com ele na saúde: a vaga que não saiu, o remédio que
            faltou, o atendimento que foi bom, a irregularidade que viu. Esta tela é o registro único dessas manifestações
            na {instituicao().nomeSecretaria}: tudo o que entra — pelo balcão, pelo telefone, pelo WhatsApp, por carta,
            pela urna, pelo Disque 136, pelo Fala.BR ou pela ouvidoria-geral do município — vira um protocolo aqui e
            segue o mesmo caminho.
          </P>
          <P>
            O trabalho da ouvidoria é <strong>receber, registrar, classificar, encaminhar, cobrar e devolver uma
            resposta</strong>. Ela não resolve o problema com as próprias mãos: quem resolve é a unidade ou a área
            responsável (o <em>ponto de resposta</em>). E ela <strong>não investiga</strong>: denúncia vai para uma
            unidade apuratória, sem o nome de quem denunciou.
          </P>
          <FluxoManifestacao />
          <Callout tipo="atencao" titulo="O que a ouvidoria NÃO é">
            Não é balcão de marcação de consulta, não é auditoria nem corregedoria, e não é o pedido de acesso à
            informação da LAI (esse tem outro fluxo e outro prazo). Uma pessoa que quer marcar exame é atendida pela
            regulação; o que a ouvidoria registra é a <em>manifestação</em> dela sobre esse atendimento.
          </Callout>
        </div>
      ),
    },
    {
      id: 'tipos',
      titulo: 'Os seis tipos',
      busca: 'tipo solicitação reclamação denúncia sugestão elogio informação definição qual escolher identificação por tipo',
      conteudo: (
        <div className="space-y-4">
          <P>
            O tipo decide duas coisas: <strong>quem pode ficar sem se identificar</strong> e{' '}
            <strong>como a resposta final é registrada</strong>. Por isso é o primeiro campo do formulário — e pode ser
            corrigido na triagem, se quem registrou errou.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: <SeloRef cor="info">Solicitação</SeloRef>,
                descricao:
                  'Contém um pedido: vaga, exame, medicamento, transporte. É o tipo mais comum. Sempre identificada — sem saber quem pede, não há como atender.',
              },
              {
                termo: <SeloRef cor="alerta">Reclamação</SeloRef>,
                descricao: 'Insatisfação com um serviço ou atendimento, sem pedido concreto. Identificada ou sigilosa.',
              },
              {
                termo: <SeloRef cor="erro">Denúncia</SeloRef>,
                descricao:
                  'Relato de irregularidade ou de indício dela. É o único tipo que aceita anônimo — e o único que passa por habilitação antes de sair da ouvidoria.',
              },
              {
                termo: <SeloRef cor="sucesso">Sugestão</SeloRef>,
                descricao: 'Ideia para melhorar um serviço. Identificada ou sigilosa.',
              },
              {
                termo: <SeloRef cor="sucesso">Elogio</SeloRef>,
                descricao:
                  'Reconhecimento a um serviço ou a um profissional. Identificada ou sigilosa. A resposta diz que chegou a quem foi elogiado e à chefia dele.',
              },
              {
                termo: <SeloRef cor="gray">Informação</SeloRef>,
                descricao: 'Pergunta sobre serviços, horários, fluxos. Sempre identificada — a resposta precisa de um destinatário.',
              },
            ]}
          />
          <Callout tipo="dica" titulo="Solicitação ou reclamação?">
            Se o texto pede alguma coisa ("preciso da consulta", "quero o remédio"), é solicitação, mesmo que venha em
            tom de queixa. Se só relata a insatisfação ("fui mal atendida"), é reclamação. A diferença importa no fim:
            solicitação é <em>atendida</em> ou <em>não atendida</em>; reclamação <em>procede</em> ou <em>não procede</em>.
          </Callout>
        </div>
      ),
    },
    {
      id: 'identificacao-e-sigilo',
      titulo: 'Identificada, sigilosa ou anônima',
      busca: 'identificação sigilosa anônima identidade restrita revelar identidade justificativa registrado quem vê o manifestante',
      conteudo: (
        <div className="space-y-4">
          <P>
            Quem manifesta escolhe quanto de si aparece. O sistema respeita a escolha em todas as telas — inclusive
            para você.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Identificada',
                descricao:
                  'Nome, CPF e contato ficam visíveis para a ouvidoria. A área que responde nunca vê esses dados, em nenhum caso — ela recebe o relato, não a pessoa.',
              },
              {
                termo: 'Sigilosa',
                descricao:
                  'Os dados existem, mas ficam guardados. Na fila e no detalhe aparece "restrito". Só quem tem o módulo Sigilo consegue revelar — e cada revelação exige uma justificativa de pelo menos 15 caracteres e fica gravada com nome e data na linha do tempo e na auditoria. O aviso por WhatsApp de uma sigilosa leva só o protocolo e a etapa, nunca o assunto ou o teor.',
              },
              {
                termo: 'Anônima',
                descricao:
                  'Nenhum dado é registrado; o formulário apaga o que estiver preenchido. Só existe em denúncia. Não gera código de acesso: o cidadão não acompanha, não complementa e não é avisado de nada. A resposta conclusiva só fica registrada e já conclui.',
              },
            ]}
          />
          <Callout tipo="lgpd" titulo="Por que revelar identidade deixa rastro">
            A identidade de quem manifesta é informação pessoal com restrição de acesso por lei. Revelar não é proibido —
            às vezes é indispensável para tratar o caso — mas precisa ser <strong>excepcional e explicável</strong>.
            O registro com seu nome, a data e a justificativa é o que permite responder, meses depois, quem viu e por quê.
          </Callout>
        </div>
      ),
    },
    {
      id: 'protocolo-e-prazos',
      titulo: 'Protocolo, código de acesso e os dois relógios',
      busca: 'protocolo AAAA-NNNNNN código de acesso mostrado uma vez prazo 30 dias prorrogação 30 dias uma vez justificativa prazo da área 20 10 2 dias úteis prioridade complementação suspende relógio',
      conteudo: (
        <div className="space-y-4">
          <Sub>O protocolo</Sub>
          <P>
            Toda manifestação nasce com um número no formato <strong>ano-sequência</strong> (ex.: <code>2026-000123</code>).
            É o comprovante de recebimento do cidadão e a chave para achar a manifestação na fila. Dá para copiar com um
            clique no detalhe.
          </P>
          <Sub>O código de acesso</Sub>
          <P>
            Junto com o protocolo, o sistema gera um código de 8 caracteres e o mostra <strong>uma única vez</strong>, na
            janela que aparece logo após registrar. O sistema guarda só uma versão cifrada: fechou a janela sem anotar, o
            código não volta. Por isso a janela avisa em amarelo — anote ou copie antes de fechar e repasse ao cidadão.
            É com ele que o cidadão prova ser o dono do protocolo para acompanhar e complementar. Anônima não tem
            código.
          </P>
          <Sub>O relógio do cidadão</Sub>
          <P>
            A ouvidoria tem <strong>30 dias</strong> (padrão da Lei 13.460, ajustável na Configuração) para dar a resposta
            conclusiva, contados do registro. Pode <strong>prorrogar uma vez</strong>, por mais 30 dias, com justificativa
            de pelo menos 20 caracteres — a justificativa vai ao cidadão. Não há segunda prorrogação, e manifestação
            encaminhada a outro órgão não prorroga.
          </P>
          <Sub>O relógio da área</Sub>
          <P>
            Quando a manifestação é encaminhada, a unidade ou área ganha o próprio prazo, conforme a prioridade definida
            na triagem: <strong>Normal 20 dias</strong>, <strong>Alta 10 dias</strong>, <strong>Urgente 2 dias úteis</strong>{' '}
            (padrões da Configuração). Um ponto de resposta pode ter prazo próprio, e quem encaminha pode informar outro
            na hora. Esse relógio serve para a ouvidoria cobrar a tempo de ainda responder no prazo do cidadão.
          </P>
          <Callout tipo="regra" titulo="Complementação suspende o relógio — uma vez">
            Se falta informação do cidadão, peça complementação: o prazo do cidadão para de contar até ele responder.
            Isso só pode ser feito <strong>uma vez por manifestação</strong>. Se em <strong>20 dias</strong> (padrão) nada
            chegar, o sistema arquiva sozinho com o motivo "sem complementação no prazo". Anônima não tem como ser
            complementada.
          </Callout>
        </div>
      ),
    },
    {
      id: 'fila',
      titulo: 'A fila: abas, filtros e a cor do prazo',
      busca: 'fila abas triagem em andamento aguardando validação atrasadas recurso concluídas filtro tipo prioridade unidade ponto de resposta buscar protocolo nome cpf verde amarelo vermelho vence hoje',
      conteudo: (
        <div className="space-y-4">
          <P>
            A fila da ouvidoria central (menu Ouvidoria › Fila) separa as manifestações por etapa. As abas são deduzidas
            do status — ninguém "move" uma manifestação de aba; ela muda quando alguém faz uma ação.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: <AbaRef>Triagem</AbaRef>,
                descricao: 'Registradas e em triagem. É o que acabou de chegar e ainda não foi a lugar nenhum. Comece por aqui.',
              },
              {
                termo: <AbaRef>Em andamento</AbaRef>,
                descricao: 'Encaminhadas à área, aguardando complementação do cidadão e respondidas pela área. O trabalho está com outra pessoa — ou com o cidadão.',
              },
              {
                termo: <AbaRef>Aguardando validação</AbaRef>,
                descricao: 'A área respondeu e a ouvidoria precisa ler, validar e escrever para o cidadão. É a sua segunda fila de trabalho.',
              },
              {
                termo: <AbaRef>Atrasadas</AbaRef>,
                descricao: 'Tudo o que passou do prazo do cidadão, em qualquer etapa. Quando está vazia, a tela diz "Ótimo".',
              },
              {
                termo: <AbaRef>Recurso</AbaRef>,
                descricao: 'O cidadão discordou da resposta e recorreu. Precisa de nova análise e nova resposta.',
              },
              {
                termo: <AbaRef>Concluídas</AbaRef>,
                descricao: 'Respondidas ao cidadão, concluídas, arquivadas e encaminhadas a outro órgão. É onde ficam "Registrar recurso" e "Concluir".',
              },
            ]}
          />
          <Sub>Os filtros</Sub>
          <Lista>
            <Item>
              <strong>Busca</strong> — protocolo, nome ou CPF do manifestante. Pode digitar parcial.
            </Item>
            <Item>
              <strong>Tipo</strong>, <strong>Prioridade</strong>, <strong>Unidade</strong> e <strong>Ponto de resposta</strong>{' '}
              — para dividir a fila entre a equipe ou olhar uma unidade só.
            </Item>
            <Item>
              <strong>Quantidade por página</strong> — 25, 50 ou 100, no rodapé.
            </Item>
          </Lista>
          <Sub>A cor do prazo</Sub>
          <P>
            Cada linha mostra o prazo do cidadão e, quando está com a área, o da área. A cor diz o que fazer sem ler a
            data:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: <SeloRef cor="sucesso">12 dias</SeloRef>, descricao: 'No prazo, com folga.' },
              { termo: <SeloRef cor="alerta">vence amanhã</SeloRef>, descricao: 'Faltam 5 dias ou menos. Cobre a área hoje.' },
              { termo: <SeloRef cor="erro">atrasada 3 dias</SeloRef>, descricao: 'Passou. Cada dia a mais entra na conta de dias de atraso da manifestação.' },
              { termo: <SeloRef cor="gray">encerrada</SeloRef>, descricao: 'Já respondida ou finalizada — o relógio não corre mais.' },
            ]}
          />
        </div>
      ),
    },
    {
      id: 'registrar',
      titulo: 'Registrar uma manifestação',
      busca: 'registrar formulário obrigatório tipo identificação canal relato teor cpf basta nome telefone nunca se recusa manifestação buscar cidadão paciente referido agente envolvido anexos sistema externo protocolo externo',
      conteudo: (
        <div className="space-y-4">
          <P>
            Menu Ouvidoria › <BotaoRef>Registrar manifestação</BotaoRef>. É o registro interno: a pessoa está na sua
            frente, no telefone ou mandou uma carta. Escreva o relato como ela contou — não existe campo "motivo", o
            texto é o registro.
          </P>
          <Callout tipo="regra" titulo="Nunca se recusa manifestação">
            Só quatro coisas são obrigatórias: <strong>tipo</strong>, <strong>identificação</strong>,{' '}
            <strong>canal de entrada</strong> e <strong>relato</strong> (mínimo 10 caracteres). Tudo o mais é opcional
            — assunto, unidade, data e local do fato, anexos. Falta de campo não é motivo para não registrar; o que
            faltar, a triagem completa depois.
          </Callout>
          <Sub>Quem manifesta</Sub>
          <P>
            Em manifestação identificada, <strong>o CPF basta</strong> (é a lei). Sem CPF, informe nome e um contato
            (telefone ou e-mail) — em solicitação e informação isso é exigido, porque a resposta precisa chegar a
            alguém. O botão de buscar cidadão já cadastrado preenche nome, CPF e telefone do cadastro. O telefone é por
            onde o cidadão recebe o protocolo e os avisos de etapa pelo WhatsApp.
          </P>
          <Sub>Em favor de quem / sobre quem</Sub>
          <P>
            Só quando a manifestação é sobre outra pessoa (a mãe que reclama pelo filho) ou aponta um agente. O paciente
            referido pode vir do cadastro. O campo "agente/serviço envolvido" descreve quem foi apontado — em denúncia,
            esse dado não vai à área na versão pseudonimizada.
          </P>
          <Sub>Complementos</Sub>
          <P>
            Se a manifestação já existe em outro sistema (OuvidorSUS, Fala.BR, ouvidoria-geral), anote o sistema e o
            protocolo de lá. Anexos aceitam a foto do documento, o áudio, a carta digitalizada.
          </P>
          <P>
            Ao salvar, a janela "Manifestação registrada" mostra o protocolo, o código de acesso e o prazo. Copie e
            repasse antes de fechar — veja a seção anterior.
          </P>
        </div>
      ),
    },
    {
      id: 'tramitar',
      titulo: 'Tramitar: da triagem à validação',
      busca: 'triar encaminhar à área ponto de resposta responder pela área devolver à área reanálise cobrar a área escalonar encaminhar a outro órgão anotar linha do tempo visível ao cidadão interno duplicidade',
      conteudo: (
        <div className="space-y-4">
          <P>
            Abra a manifestação na fila. Os botões que aparecem no topo dependem do status e do seu perfil; se um botão
            deste manual não aparece, é porque a manifestação não está na etapa certa ou o seu perfil não tem a ação.
            O ciclo normal é este:
          </P>
          <Passos
            itens={[
              {
                titulo: (
                  <>
                    <BotaoRef>Triar</BotaoRef> — classificar
                  </>
                ),
                detalhe:
                  'Corrija o tipo se preciso (respeitando a identificação: uma denúncia anônima não vira solicitação), escolha assunto e subassunto, unidade, prioridade, marcadores e um resumo de uma linha para a fila. A prioridade define o prazo da área — mas só enquanto não encaminhada.',
              },
              {
                titulo: (
                  <>
                    <BotaoRef>Encaminhar à área</BotaoRef> — mandar a quem resolve
                  </>
                ),
                detalhe:
                  'Escolha o ponto de resposta (unidade, área central ou apuração). Pode informar um prazo em dias e uma orientação interna. O cidadão recebe só "encaminhada à área responsável", sem nomes. Denúncia só encaminha depois de habilitada, só para unidade apuratória e só com o teor pseudonimizado preenchido.',
              },
              {
                titulo: 'A área responde',
                detalhe:
                  'O membro do ponto de resposta (tela Meu ponto) escreve o que apurou e o que fez, com anexos se quiser. Se a área não tem acesso ao sistema, quem tem o módulo Ouvidoria pode usar "Responder pela área" para registrar o que ela mandou por outro meio. Essa resposta é interna: o cidadão não a vê.',
              },
              {
                titulo: (
                  <>
                    Validar — ou <BotaoRef variante="outline">Devolver à área</BotaoRef>
                  </>
                ),
                detalhe:
                  'Leia a resposta na aba Aguardando validação. Se está vaga, incompleta ou não responde ao que foi perguntado, devolva para reanálise dizendo o que falta: a área ganha metade do prazo original (mínimo 2 dias). Se está boa, responda ao cidadão (seção seguinte).',
              },
            ]}
          />
          <Sub>Quando a área não responde</Sub>
          <ListaDefinicoes
            itens={[
              {
                termo: <BotaoRef variante="outline">Cobrar a área</BotaoRef>,
                descricao:
                  'Registra a cobrança na linha do tempo, onde os membros do ponto veem. Fica documentado que a ouvidoria cobrou — é o que sustenta um escalonamento depois.',
              },
              {
                termo: <BotaoRef variante="outline">Escalonar</BotaoRef>,
                descricao:
                  'Ação da gestão: registra que a manifestação subiu de nível, para quem e por quê. Interna.',
              },
            ]}
          />
          <Sub>Outros caminhos</Sub>
          <ListaDefinicoes
            itens={[
              {
                termo: <BotaoRef variante="outline">Pedir complementação</BotaoRef>,
                descricao:
                  'Quando falta algo que só o cidadão sabe. Suspende o prazo e vale uma vez. Quando a resposta chegar (por telefone, balcão, e-mail), use "Registrar complementação" — o relógio volta a contar e a manifestação retorna à triagem.',
              },
              {
                termo: <BotaoRef variante="outline">Encaminhar a outro órgão</BotaoRef>,
                descricao:
                  'O assunto não é da saúde municipal. Informe o órgão ou sistema de destino, o protocolo de lá (se houver) e um texto ao cidadão dizendo para onde foi e como acompanhar. Encerra a manifestação aqui e bloqueia prorrogação.',
              },
              {
                termo: <BotaoRef variante="ghost">Anotar</BotaoRef>,
                descricao: 'Nota interna em qualquer etapa. O cidadão não vê.',
              },
            ]}
          />
          <Callout tipo="dica" titulo="Leia a linha do tempo pelos olhos do cidadão">
            Cada evento traz um selo: <SeloRef cor="info">visível ao cidadão</SeloRef> ou <SeloRef cor="gray">interno</SeloRef>.
            Registro, encaminhamento, pedido de complementação, prorrogação, respostas ao cidadão, recurso, conclusão e
            arquivamento ele vê; resposta da área, devolução, cobrança, escalonamento, anotação e habilitação de
            denúncia, não. Se um aviso amarelo de <strong>possível duplicidade</strong> aparecer no topo (mesmo CPF,
            assunto e unidade em 90 dias), confira os protocolos citados antes de encaminhar — arquivar por duplicidade é
            decisão sua, não do sistema.
          </Callout>
        </div>
      ),
    },
    {
      id: 'responder-e-concluir',
      titulo: 'Responder ao cidadão e concluir',
      busca: 'responder ao cidadão intermediária conclusiva conteúdo mínimo por tipo resolvida não resolvida resolutividade situação final atendida não atendida procede não procede inconclusiva motivo do não atendimento recurso uma vez concluir conclusão automática 30 dias',
      conteudo: (
        <div className="space-y-4">
          <P>
            <BotaoRef>Responder ao cidadão</BotaoRef> tem dois modos, e a diferença é o que acontece com o relógio.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Resposta intermediária',
                descricao:
                  'Um "estamos cuidando, a área foi acionada em tal data". Vai ao acompanhamento do cidadão, mas o status não muda e o prazo continua correndo. Mínimo 10 caracteres.',
              },
              {
                termo: 'Resposta conclusiva',
                descricao:
                  'A resposta de mérito. Muda o status para "Respondida ao cidadão", para o relógio e exige resolutividade e situação final. Mínimo 20 caracteres.',
              },
            ]}
          />
          <Sub>O que a resposta conclusiva precisa dizer</Sub>
          <P>
            O sistema não bloqueia por conteúdo — mas mostra, no campo, o mínimo esperado para cada tipo (é a regra da
            Portaria Normativa CGU 116). Use como roteiro:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Solicitação', descricao: 'A providência adotada — ou, se ainda não foi possível, a possibilidade, a forma e o meio de atendimento.' },
              { termo: 'Reclamação', descricao: 'A análise do fato relatado e as providências adotadas (ou por que não foram).' },
              { termo: 'Denúncia', descricao: 'Se foi encaminhada à unidade apuratória competente ou arquivada — e, no arquivamento, o motivo.' },
              { termo: 'Sugestão', descricao: 'A posição do gestor (acatada, em estudo, não acatada e por quê) e, se for adotar, o prazo estimado.' },
              { termo: 'Elogio', descricao: 'Que o elogio foi encaminhado ao agente elogiado e à chefia imediata dele.' },
              { termo: 'Informação', descricao: 'A informação pedida, clara e completa. Se não for possível, o motivo e onde obtê-la.' },
            ]}
          />
          <Sub>Resolutividade e situação final</Sub>
          <P>
            <strong>Resolvida</strong> ou <strong>não resolvida</strong> é a pergunta que alimenta o indicador de
            resolutividade do painel — responda pelo que aconteceu com o cidadão, não pelo esforço da área. A{' '}
            <strong>situação final</strong> depende do tipo:
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Solicitação',
                descricao:
                  'Atendida · Não atendida (com motivo: falta de recursos, não coberto pelo SUS, fez pelo particular, vagas insuficientes, não compareceu, outro) · Cidadão não localizado · Cidadão faleceu.',
              },
              { termo: 'Reclamação e Denúncia', descricao: 'Procede · Não procede · Inconclusiva.' },
              { termo: 'Sugestão, Elogio e Informação', descricao: 'Atendida.' },
            ]}
          />
          <Sub>Depois da resposta</Sub>
          <Lista>
            <Item>
              <BotaoRef variante="outline">Registrar recurso</BotaoRef> — o cidadão discordou. Registre as razões dele;
              a manifestação vai para a aba Recurso e volta a tramitar (pode ser encaminhada de novo e respondida de
              novo). <strong>Só cabe um recurso</strong> por manifestação.
            </Item>
            <Item>
              <BotaoRef>Concluir</BotaoRef> — encerra o ciclo de uma manifestação respondida sem recurso. Se ninguém
              clicar, o sistema conclui sozinho após <strong>30 dias</strong> (padrão) da resposta.
            </Item>
          </Lista>
          <Callout tipo="regra" titulo="Anônima: responder já conclui">
            Em denúncia anônima não há a quem avisar. A resposta conclusiva fica registrada para a trilha e a manifestação
            vai direto para Concluída, sem prazo de recurso.
          </Callout>
        </div>
      ),
    },
    {
      id: 'denuncia',
      titulo: 'Denúncia: habilitar, pseudonimizar, apurar',
      busca: 'denúncia habilitar admissibilidade autoria materialidade competência teor pseudonimizado unidade apuratória apuração a ouvidoria não investiga quem vê a denúncia módulo sigilo',
      conteudo: (
        <div className="space-y-4">
          <P>
            Denúncia é o tipo com mais proteção, porque expõe duas pessoas: quem denuncia e quem é denunciado. Por isso
            ela só aparece na fila para quem tem o módulo <strong>Sigilo</strong>, e não sai da ouvidoria sem dois passos
            a mais.
          </P>
          <Passos
            itens={[
              {
                titulo: (
                  <>
                    <BotaoRef variante="outline">Habilitar denúncia</BotaoRef> — a análise de admissibilidade
                  </>
                ),
                detalhe:
                  'Quem tem Sigilo registra, em texto interno, por que a denúncia segue: há autoria (de quem se fala), materialidade (o que aconteceu) e competência (é assunto nosso). Enquanto não habilitada, o detalhe mostra um aviso vermelho e o botão de encaminhar não a aceita.',
              },
              {
                titulo: (
                  <>
                    <BotaoRef variante="outline">Editar teor pseudonimizado</BotaoRef> — a versão que sai
                  </>
                ),
                detalhe:
                  'Reescreva o relato sem nomes, contatos ou qualquer pista de quem denunciou. É esta versão que a unidade apuratória recebe; o relato original fica só na ouvidoria. Sem ela preenchida, o encaminhamento é recusado.',
              },
              {
                titulo: 'Encaminhar à unidade apuratória',
                detalhe:
                  'Denúncia só vai para ponto de resposta do tipo "Unidade apuratória (denúncias)". Lá, o membro vê o teor pseudonimizado, não vê o manifestante e não vê o campo "agente/serviço envolvido".',
              },
            ]}
          />
          <Callout tipo="atencao" titulo="A ouvidoria não investiga">
            Ouvir testemunha, pedir documento, fazer diligência: nada disso é papel da ouvidoria — é da unidade
            apuratória (corregedoria, comissão de ética, conselho profissional). O que a ouvidoria faz é habilitar,
            pseudonimizar, encaminhar, cobrar e responder ao denunciante se a denúncia foi encaminhada ou arquivada.
          </Callout>
        </div>
      ),
    },
    {
      id: 'meu-ponto',
      titulo: 'Meu ponto de resposta: a tela de quem responde pela área',
      busca: 'meu ponto membro ponto de resposta aguardando minha área respondidas pela área todas do meu ponto não vê manifestante responder pela área anexos',
      conteudo: (
        <div className="space-y-4">
          <P>
            Quem responde por uma unidade, área central ou unidade apuratória não usa a fila da ouvidoria: usa{' '}
            <strong>Ouvidoria › Meu ponto</strong>. A tela mostra só o que foi encaminhado aos pontos de que a pessoa é
            membro, em três recortes: <AbaRef>Aguardando minha área</AbaRef>, <AbaRef>Respondidas pela área</AbaRef> e{' '}
            <AbaRef>Todas do meu ponto</AbaRef>. Busca por protocolo.
          </P>
          <P>
            Ao abrir uma manifestação dali, o membro vê o relato (em denúncia, a versão pseudonimizada), a
            classificação, os prazos e a linha do tempo do próprio ponto — e{' '}
            <strong>nunca vê quem manifestou</strong>: não há card de manifestante, e a coluna não existe na tabela. A
            única ação dele é <BotaoRef>Responder pela área</BotaoRef>: dizer o que foi apurado e o que foi feito, com
            anexos se quiser.
          </P>
          <Callout tipo="dica" titulo="Por que a área não vê o cidadão">
            Não é desconfiança da área. É que a resposta deve ser sobre o serviço, não sobre a pessoa — e quem reclama
            de uma unidade precisa poder voltar a ela sem medo. A ouvidoria é quem fala com o cidadão; a área fala com a
            ouvidoria.
          </Callout>
        </div>
      ),
    },
    {
      id: 'gestao',
      titulo: 'Gestão: pontos de resposta, assuntos, configuração e painel',
      busca: 'pontos de resposta titular membro prazo próprio unidade área central apuração assuntos dois níveis código OuvidorSUS marcadores configuração prazos avisos whatsapp texto do recibo painel indicadores no prazo tempo médio estoque resolutividade',
      conteudo: (
        <div className="space-y-4">
          <Sub>Pontos de resposta</Sub>
          <P>
            É o cadastro de quem responde. Cada ponto tem um tipo — <strong>Unidade de saúde</strong> (ligado a uma
            unidade; cada unidade só pode ter um ponto), <strong>Área central</strong> (farmácia, regulação, transporte…)
            ou <strong>Unidade apuratória</strong> (recebe denúncias habilitadas) — e uma lista de membros, usuários do
            sistema que precisam ter o módulo "Ouvidoria — ponto de resposta" no perfil. Um membro pode ser marcado como{' '}
            <strong>titular</strong>: é a referência para cobranças. O ponto pode ter <strong>prazo próprio</strong> em
            dias; em branco, vale o prazo da configuração por prioridade. Ponto inativo não aparece para encaminhamento.
            A coluna "Em aberto" mostra quantas manifestações encaminhadas cada ponto ainda não respondeu.
          </P>
          <Callout tipo="atencao" titulo="Sem membro, ninguém recebe">
            Um ponto sem membros existe, aceita encaminhamento — e ninguém vê a manifestação. Ao criar o ponto, adicione
            ao menos um membro e marque o titular.
          </Callout>
          <Sub>Assuntos e marcadores</Sub>
          <P>
            <strong>Assuntos</strong> têm dois níveis (assunto › subassunto), ordem de exibição e, opcionalmente, o
            código correspondente do OuvidorSUS — útil para bater relatórios com o sistema nacional. Desativar um assunto
            o tira dos formulários sem apagar o histórico. <strong>Marcadores</strong> são etiquetas livres, para o que a
            equipe quiser acompanhar transversalmente ("campanha X", "recorrente").
          </P>
          <Sub>Configuração</Sub>
          <P>
            Os prazos desta instância: resposta ao cidadão (30), prorrogação (30), prazo da área por prioridade
            (20 / 10 / 2 dias úteis), complementação (20) e conclusão automática após a resposta (30). E os avisos por
            WhatsApp: ligar ou desligar, e o texto do recibo com <code>{'{protocolo}'}</code> e <code>{'{prazo}'}</code>.
            Nunca há aviso em anônima; em sigilosa, só protocolo e etapa.
          </P>
          <Sub>Painel</Sub>
          <P>
            Por período e, se quiser, por unidade: registradas, respondidas, percentual no prazo, tempo médio de resposta
            (e o da área), estoque em aberto e resolutividade; gráficos por tipo, status, assuntos mais frequentes e
            faixas de tempo até a resposta (até 30, 31 a 60, mais de 60 dias). É a base do relatório de gestão que a lei
            manda publicar.
          </P>
        </div>
      ),
    },
    {
      id: 'quem-pode-o-que',
      titulo: 'Quem pode o quê',
      busca: 'permissão perfil módulo Ouvidoria OuvidoriaGestao OuvidoriaSigilo OuvidoriaPontoResposta consulta inclusão edição exclusão não aparece botão',
      conteudo: (
        <div className="space-y-4">
          <P>
            A ouvidoria tem <strong>quatro módulos</strong> no perfil, porque são quatro papéis diferentes: tratar a
            manifestação, gerir o serviço, ver o que é sigiloso e responder pela área. Uma pessoa pode ter mais de um.
          </P>
          <TabelaPermissoes
            linhas={[
              {
                modulo: 'Ouvidoria',
                acoes: [
                  'Ver fila e detalhe (manifestante mascarado se sigilosa; denúncias só com Sigilo)',
                  'Registrar manifestação',
                  'Triar, encaminhar, pedir complementação, validar, responder ao cidadão, prorrogar, cobrar, recurso, concluir',
                  'Arquivar manifestação (com motivo)',
                ],
              },
              {
                modulo: 'Ouvidoria — gestão',
                acoes: [
                  'Ver painel e configuração',
                  'Criar pontos de resposta, assuntos e marcadores',
                  'Editar pontos, assuntos, marcadores e configuração; escalonar',
                  'Desativar pontos, assuntos e marcadores',
                ],
              },
              {
                modulo: 'Ouvidoria — sigilo',
                acoes: [
                  'Ver denúncias e revelar identidade em sigilosas (com justificativa registrada)',
                  'Habilitar denúncia (admissibilidade)',
                  'Editar o teor pseudonimizado que vai à apuração',
                  '—',
                ],
              },
              {
                modulo: 'Ouvidoria — ponto de resposta',
                acoes: ['Ver o que foi encaminhado aos seus pontos, sem dados do manifestante', '—', 'Responder pela área (com anexos)', '—'],
              },
            ]}
          />
          <P>
            "Exclusão" na Ouvidoria é <strong>arquivar</strong>, não apagar: a trilha é permanente. Se um botão citado
            aqui não aparece para você, quase sempre é permissão — peça ao responsável pelo seu perfil ou abra um ticket
            no menu Suporte.
          </P>
        </div>
      ),
    },
    {
      id: 'duvidas-frequentes',
      titulo: 'Dúvidas que sempre aparecem',
      busca: 'faq dúvidas perdeu o código responder sem passar pela área reclamação anônima prazo estourou vaga do sisreg quem vê a denúncia apagar manifestação',
      conteudo: (
        <div className="space-y-3">
          <ListaDefinicoes
            itens={[
              {
                termo: 'O cidadão perdeu o código de acesso',
                descricao:
                  'O sistema não tem como mostrá-lo de novo — só guarda a versão cifrada. Confirme a identidade dele (CPF, protocolo) e informe o andamento pela ouvidoria; o que ele quiser complementar, registre com "Registrar complementação".',
              },
              {
                termo: 'Posso responder ao cidadão sem passar pela área?',
                descricao:
                  'Pode: "Responder ao cidadão" está disponível já na triagem. Faz sentido em informação simples, elogio, ou quando a ouvidoria mesma tem a resposta. Para reclamação e solicitação sobre uma unidade, a resposta de mérito é da área — encaminhe.',
              },
              {
                termo: 'A pessoa quer fazer uma reclamação anônima',
                descricao:
                  'Anônimo só existe em denúncia. Ofereça a sigilosa: a área não saberá quem é, só a ouvidoria — e ela ainda recebe protocolo, código e resposta. Se o relato for de irregularidade, aí sim é denúncia e pode ser anônima.',
              },
              {
                termo: 'O prazo estourou. E agora?',
                descricao:
                  'A manifestação está na aba Atrasadas e conta dias de atraso. Se ainda não prorrogou, prorrogue com justificativa (uma vez). Se a área não respondeu, cobre e, se preciso, escalone. Atraso não impede responder — impede é ficar sem resposta.',
              },
              {
                termo: 'A manifestação é sobre uma vaga do SISREG',
                descricao:
                  'É uma solicitação, identificada, com assunto de regulação e a área central de regulação como ponto de resposta. A ouvidoria não marca a vaga — cobra a resposta de quem marca, e devolve ao cidadão a possibilidade, a forma e o meio de atendimento.',
              },
              {
                termo: 'Quem vê a denúncia?',
                descricao:
                  'Na fila, só quem tem o módulo Sigilo. A unidade apuratória vê o teor pseudonimizado, sem manifestante e sem o campo de agente envolvido. Quem tem só Ouvidoria não a vê na lista.',
              },
              {
                termo: 'Posso apagar uma manifestação registrada por engano?',
                descricao:
                  'Não. Nada se apaga: arquive com o motivo certo (duplicidade com o protocolo original, texto incompreensível, imprópria, cópia para conhecimento…). O cidadão vê "arquivada" e o motivo; a trilha fica.',
              },
              {
                termo: 'Registrei com o tipo errado',
                descricao:
                  'Corrija em "Triar". A troca respeita a identificação: uma anônima só pode continuar denúncia; uma sigilosa não vira solicitação nem informação.',
              },
            ]}
          />
        </div>
      ),
    },
  ],
};
