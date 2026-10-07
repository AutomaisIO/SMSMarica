import {
  CircleDashed,
  Contrast,
  Crosshair,
  Edit2,
  FilePlus,
  FileText,
  FlipHorizontal2,
  Hand,
  History,
  Link2,
  Maximize2,
  MessageSquarePlus,
  RotateCcw,
  Ruler,
  Save,
  ScanLine,
  ScanSearch,
  Spline,
  SunMoon,
  Trash2,
  Unlink,
  ZoomIn,
  type LucideIcon,
} from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import { TelaSimulada } from '@/features/manual/components/TelaSimulada';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo de Exames de imagem (`/app/pacs`, menu "Abrir Exame") e do visualizador que abre em
 * janela separada (`/pacs/janela`).
 *
 * A ordem segue a dúvida de quem chega: o que é → achar o exame → ler a lista → o que fazer na
 * linha → abrir o visualizador → o cabeçalho (com o CID do pedido) → imagens e grade →
 * ferramentas → anotações → casos chatos → quem pode o quê → dúvidas.
 *
 * Conferido em `features/pacs` (PacsListagemPage, PacsViewerPage, PacsViewport,
 * PacsViewportCelula, PacsImagensSidebar, SeletorLayoutGrade, PacsHistoricoAnotacoesModal,
 * ModalAssociarExame) e no backend da associação (`ExameAssociacaoService`).
 */

/** Ícone da barra do visualizador, na mesma cor escura da tela, para citar no texto. */
function IconeBarra({ icone: Icone }: { icone: LucideIcon }) {
  return (
    <span className="mx-0.5 inline-flex h-6 w-6 items-center justify-center rounded-md bg-gray-900 align-middle text-gray-200">
      <Icone className="h-3.5 w-3.5" />
    </span>
  );
}

/** Ícone de ação da linha da lista, com a cor que ele tem lá. */
function IconeLinha({ icone: Icone, cor }: { icone: LucideIcon; cor: string }) {
  return (
    <span className={`mx-0.5 inline-flex align-middle ${cor}`}>
      <Icone className="h-4 w-4" />
    </span>
  );
}

/** Desenho do cabeçalho do visualizador — mesmas cores e o mesmo badge da tela real. */
function CabecalhoExemplo({ comCid }: { comCid: boolean }) {
  return (
    <div className="flex min-w-0 items-center gap-3 rounded-md border border-gray-700 bg-gray-900 px-4 py-2 text-sm text-gray-300">
      <span className="min-w-0 truncate">
        <span className="font-medium text-white">MARIA DE TESTE (exemplo)</span>
        <span className="text-gray-500"> · </span>
        052Y/F
        <span className="text-gray-500"> · </span>
        ULTRASSONOGRAFIA MAMARIA BILATERAL
        <span className="text-gray-500"> · </span>
        06/10/2026
      </span>
      {comCid ? (
        <span className="inline-flex min-w-0 max-w-[28rem] shrink items-center rounded-full bg-amber-400/15 px-2.5 py-0.5 text-xs text-amber-100 ring-1 ring-inset ring-amber-400/50">
          <span className="truncate">
            <span className="font-semibold text-amber-300">CID N63</span> — Nódulo mamário não
            especificado
          </span>
        </span>
      ) : null}
    </div>
  );
}

export const artigoPacs: Artigo = {
  slug: 'pacs',
  titulo: 'Exames de imagem e visualizador (PACS)',
  resumo:
    'Achar o exame que chegou do aparelho, ligá-lo ao pedido certo e abrir as imagens no visualizador — com o diagnóstico inicial (CID) do pedido no alto, ferramentas de medida e anotações salvas por versão.',
  grupo: 'assistencial',
  icone: ScanLine,
  rota: '/app/pacs',
  publico: 'Quem lauda, quem confere a chegada das imagens e quem associa exame ao pedido',
  atualizadoEm: '2026-10-07',
  palavrasChave: [
    'PACS',
    'visualizador',
    'viewer',
    'abrir exame',
    'exames de imagem',
    'imagem',
    'DICOM',
    'janela separada',
    'popup',
    'CID',
    'CID-10',
    'diagnóstico inicial',
    'badge',
    'cabeçalho',
    'mamografia',
    'ultrassom',
    'raio-x',
    'modalidade',
    'tipo de exame',
    'associar',
    'desassociar',
    'associação',
    'sem pedido',
    'órfão',
    'resincronizar',
    'worklist',
    'auto',
    'urgente',
    'laudar',
    'criar laudo',
    'PDF do laudo',
    'excluir exame',
    'janela nível',
    'contraste',
    'zoom',
    'lupa',
    'régua',
    'medir',
    'ângulo',
    'ROI',
    'intensidade do pixel',
    'comentário',
    'seta',
    'girar',
    'espelhar',
    'negativo',
    'inverter',
    'resetar',
    'grade',
    'dividir a tela',
    'comparar imagens',
    'miniatura',
    'anotações',
    'salvar anotações',
    'histórico de anotações',
    'versão',
    'calibração',
    'mm/px',
    'imagem em branco',
    'recriar imagem',
    'sem compressão',
    'resolução diagnóstica',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'PACS abrir exame exames de imagem visualizador janela separada servidor de imagens aparelho',
      conteudo: (
        <>
          <P>
            Todo exame de imagem feito na rede — mamografia, ultrassom, raio‑X — sai do aparelho e vai
            para o <strong>PACS</strong>, o servidor que guarda as imagens. Esta tela é a porta de
            entrada para elas: em <strong>Exames de Imagem → Abrir Exame</strong> aparece a lista do
            que está no PACS, e um clique na linha abre o <strong>visualizador</strong> numa janela
            separada do navegador.
          </P>
          <P>
            A lista não mostra só o que o aparelho escreveu. Ela cruza cada exame com o{' '}
            <strong>pedido</strong> que o originou — o número SMS, o paciente do nosso cadastro, o
            procedimento do SISREG, a unidade, se tem anamnese e se já tem laudo. É esse cruzamento que
            permite laudar com segurança: a imagem certa, do paciente certo, para o pedido certo.
          </P>
          <Callout tipo="regra" titulo="Janela separada, de propósito">
            O visualizador abre fora do painel para poder ir para um segundo monitor e ficar aberto
            enquanto o laudo é escrito na janela principal. Cada exame abre a sua própria janela.
          </Callout>
        </>
      ),
    },
    {
      id: 'achar-o-exame',
      titulo: 'Achar o exame',
      busca:
        'filtro nome do paciente modo qualquer ocorrência somente início data inicial final período hoje 5 dias 10 dias 30 dias 60 dias todo o período modalidade tipo de exame limite página próxima anterior',
      conteudo: (
        <>
          <P>
            A busca é <strong>ao vivo</strong>: basta mudar um filtro e, meio segundo depois, a lista se
            atualiza — não há botão de buscar.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Nome do paciente + Modo',
                descricao: (
                  <>
                    <em>Qualquer ocorrência</em> acha o nome em qualquer parte (“SILVA” acha “MARIA DA
                    SILVA”); <em>Somente início</em> só acha quem começa com o que foi digitado. É o
                    nome como o aparelho gravou.
                  </>
                ),
              },
              {
                termo: 'Data inicial / final e os atalhos de Período',
                descricao: (
                  <>
                    <SeloRef>Hoje</SeloRef>, <SeloRef>5 dias</SeloRef>, <SeloRef>10 dias</SeloRef>…
                    preenchem as duas datas de uma vez, contando hoje. <SeloRef>Todo o período</SeloRef>{' '}
                    limpa as datas. Sem data nenhuma, a lista traz os exames mais recentes, de quando
                    forem.
                  </>
                ),
              },
              {
                termo: 'Modalidade e Tipo de exame',
                descricao: (
                  <>
                    Recortam por MG, US, CR… e pelo procedimento do pedido. Ficam salvos{' '}
                    <strong>no seu usuário</strong>: quem só lauda mamografia abre a tela já filtrada,
                    em qualquer computador.
                  </>
                ),
              },
              {
                termo: 'Limite',
                descricao:
                  'Quantos exames por página (5, 10, 50 ou 100). O PACS não informa o total, então a paginação mostra só “Página N” e libera “Próxima” enquanto a página vier cheia.',
              },
            ]}
          />
          <P>
            Nome, datas e limite ficam lembrados <strong>neste navegador</strong>: ao voltar à tela, ela
            abre com o último filtro usado.
          </P>
          <Callout tipo="atencao" titulo="Filtro de tipo esconde exame sem pedido">
            O tipo de exame vem do pedido. Exame que ainda não foi ligado a pedido nenhum não tem tipo,
            e por isso some quando o filtro de tipo está ligado — a tela avisa quantos ficaram de fora.
            Para vê‑los, limpe o filtro de tipo.
          </Callout>
        </>
      ),
    },
    {
      id: 'ler-a-lista',
      titulo: 'Como ler a lista',
      busca:
        'coluna pedido número SMS SISREG sem pedido accession paciente exame data hora urgente assoc auto worklist associado manualmente automaticamente DICOM nome diferente phantom',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Pedido',
                descricao: (
                  <>
                    O nº SMS do pedido (clique para copiar) e, embaixo, o nº do SISREG quando o pedido
                    veio de lá. <em>Sem pedido</em> quer dizer que o exame ainda não foi ligado a
                    nenhum; o número cinza abaixo é o que o aparelho escreveu, e quase sempre{' '}
                    <strong>não</strong> é um número de pedido.
                  </>
                ),
              },
              {
                termo: 'Paciente',
                descricao: (
                  <>
                    Com pedido, o nome é o do <strong>nosso cadastro</strong>. Se o aparelho gravou um
                    nome diferente, ele aparece embaixo como <em>DICOM: …</em> — vale conferir. O selo
                    verde diz como a ligação foi feita: <SeloRef cor="sucesso">assoc.</SeloRef> (pela
                    worklist ou por uma pessoa) ou <SeloRef cor="sucesso">auto</SeloRef> (o sistema
                    casou sozinho pelo número). Pedido urgente ganha{' '}
                    <SeloRef cor="erro">Urgente</SeloRef> e sobe para o topo da página.
                  </>
                ),
              },
              {
                termo: 'Exame',
                descricao: (
                  <>
                    O procedimento do pedido e, embaixo, a modalidade e a unidade. Sem pedido, sobra o
                    texto genérico do aparelho (“ULTRASSONOGRAFIA”) e a unidade do equipamento que
                    enviou as imagens. Passe o mouse para ver o que o aparelho escreveu quando ele
                    discorda do pedido.
                  </>
                ),
              },
              { termo: 'Data / Hora', descricao: 'Quando o exame foi feito no aparelho.' },
            ]}
          />
          <P>
            Imagens de calibração do aparelho (paciente “PHANTOM”) não são pacientes e nunca aparecem
            na lista.
          </P>
        </>
      ),
    },
    {
      id: 'acoes-da-linha',
      titulo: 'O que dá para fazer em cada linha',
      busca:
        'ações anamnese associar desassociar laudar criar laudo rascunho editar PDF excluir resincronizar número da solicitação vincular reescrever',
      conteudo: (
        <>
          <P>Os ícones à direita da linha (o texto aparece ao passar o mouse):</P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Anamnese',
                descricao:
                  'Abre a anamnese do pedido só para leitura. Quem lauda lê o que a paciente respondeu; quem preenche é a recepção, pela lista de Exames e consultas.',
              },
              {
                termo: <IconeLinha icone={Link2} cor="text-indigo-600" />,
                descricao: (
                  <>
                    <strong>Associar</strong> o exame a um pedido. Digite o número da solicitação; a
                    janela mostra paciente, exame, unidade e CPF do pedido para você conferir antes de
                    confirmar. Ao associar, o próprio arquivo no PACS é corrigido com a identidade do
                    pedido — não é só uma marcação do nosso lado.
                  </>
                ),
              },
              {
                termo: <IconeLinha icone={Unlink} cor="text-gray-500" />,
                descricao:
                  'Desassociar. Só para associação feita por uma pessoa, e só enquanto o laudo não estiver assinado.',
              },
              {
                termo: <IconeLinha icone={FilePlus} cor="text-green-700" />,
                descricao:
                  'Criar o laudo deste exame. Fica apagado enquanto o pedido não tiver anamnese — a regra pode ser afrouxada em Configuração de Laudo.',
              },
              {
                termo: <IconeLinha icone={Edit2} cor="text-amber-600" />,
                descricao: 'Abrir o laudo que já existe (rascunho ou finalizado).',
              },
              {
                termo: <IconeLinha icone={FileText} cor="text-gray-500" />,
                descricao: 'PDF do laudo finalizado, em janela separada.',
              },
              {
                termo: <IconeLinha icone={Trash2} cor="text-red-600" />,
                descricao: (
                  <>
                    Excluir o exame <strong>do PACS</strong>, imagens inclusive. Não tem volta — só
                    para exame de teste ou duplicado.
                  </>
                ),
              },
            ]}
          />
          <P>
            No alto da tela, <BotaoRef variante="outline">Resincronizar</BotaoRef> varre os exames dos
            últimos 30 dias no PACS e liga ao pedido os que têm número reconhecível. É seguro rodar
            quantas vezes quiser: o que já está ligado não muda.
          </P>
          <Callout tipo="regra" titulo="Laudo assinado trava a associação">
            Depois que o laudo é assinado, o exame não pode mais ser associado nem desassociado: o
            documento assinado já diz de quem é aquela imagem.
          </Callout>
        </>
      ),
    },
    {
      id: 'abrir-o-visualizador',
      titulo: 'Abrir o visualizador',
      busca: 'clique na linha janela bloqueada popups liberar fechar janela confirmar sair',
      conteudo: (
        <>
          <Passos
            itens={[
              { titulo: 'Clique em qualquer parte da linha do exame (fora dos ícones).' },
              {
                titulo: 'Uma janela nova abre, já carregando o estudo.',
                detalhe:
                  'Se nada abrir e aparecer o aviso de janela bloqueada, libere os popups deste site no navegador (ícone na barra de endereço) e clique de novo.',
              },
              {
                titulo: 'A primeira imagem entra sozinha no quadro; as demais ficam na coluna da esquerda.',
              },
            ]}
          />
          <P>
            Ao fechar a janela do visualizador, o navegador pergunta se você quer mesmo sair — é para
            que um clique sem querer no “X” não jogue fora o trabalho de quem está medindo.
          </P>
        </>
      ),
    },
    {
      id: 'cabecalho',
      titulo: 'O cabeçalho: paciente, exame e diagnóstico inicial (CID)',
      busca:
        'cabeçalho nome idade sexo procedimento data CID CID-10 diagnóstico inicial badge descrição SISREG tipo de laudo amarelo',
      conteudo: (
        <>
          <P>
            A faixa no alto da janela identifica o que está aberto: nome do paciente, idade/sexo, o
            procedimento do pedido e a data do exame. No fim dela, num selo amarelo, vem o{' '}
            <strong>diagnóstico inicial</strong> — o CID‑10 que o médico solicitante informou no
            SISREG, com a descrição por extenso.
          </P>
          <TelaSimulada titulo="Exemplo" descricao="Desenho do cabeçalho com dados inventados — nada aqui é clicável.">
            <div className="space-y-2">
              <CabecalhoExemplo comCid />
              <CabecalhoExemplo comCid={false} />
            </div>
          </TelaSimulada>
          <P>
            É a mesma informação que aparece no alto da anamnese: diz a quem lauda o que o colega
            suspeitou, e com isso o tipo de laudo esperado. Descrição longa é cortada com “…”;
            passe o mouse sobre o selo para ler inteira.
          </P>
          <Sub>Quando o selo não aparece</Sub>
          <Lista>
            <Item>
              O exame ainda está <em>Sem pedido</em> — sem pedido não há de onde tirar o CID. Associe e
              reabra o visualizador.
            </Item>
            <Item>O pedido não trouxe CID (acontece com pedido que não veio do SISREG).</Item>
          </Lista>
          <P>
            Se aparecer só o código, sem descrição, o código não está no catálogo de CIDs do sistema —
            o código continua valendo.
          </P>
        </>
      ),
    },
    {
      id: 'imagens-e-grade',
      titulo: 'Imagens, grade e quadro em foco',
      busca:
        'miniaturas coluna imagens lateralidade D E CC MLO grade dividir a tela 2x2 comparar quadro em foco borda clique numa imagem vazio carregando resolução diagnóstica barra de progresso zoom mm/px calibração equipamento estimada ausente régua escala',
      conteudo: (
        <>
          <P>
            A coluna <strong>Imagens</strong>, à esquerda, lista todas as imagens do estudo. Na
            mamografia, cada uma vem rotulada com o lado e a incidência (<em>D CC</em>,{' '}
            <em>E MLO</em>). Clicar numa miniatura põe a imagem no <strong>quadro em foco</strong> — o
            que tem a borda colorida.
          </P>
          <P>
            Para comparar, use o botão de grade na barra (ele mostra o tamanho atual, como{' '}
            <em>1×1</em>) e escolha até 6×6 quadros. Clique num quadro para focá‑lo e depois na
            miniatura que deve ir para ele — por exemplo, as duas incidências craniocaudais lado a
            lado.
          </P>
          <Lista>
            <Item>
              Enquanto a imagem completa baixa, aparece uma prévia com o aviso{' '}
              <SeloRef cor="alerta">Carregando resolução diagnóstica…</SeloRef>. Não meça nem laude
              sobre a prévia: espere o aviso sumir.
            </Item>
            <Item>
              A linha fina logo abaixo da barra de ferramentas é o restante do estudo baixando em
              segundo plano; quando some, todas as imagens já abrem na hora.
            </Item>
            <Item>
              No canto do quadro ficam o zoom, o tamanho do pixel em milímetros e a{' '}
              <strong>calibração</strong>: <em>equipamento</em> (o aparelho informou — medida
              confiável), <em>estimada</em> (em amarelo: o sistema deduziu pelo detector — trate a
              medida como aproximada) ou <em>ausente</em> (sem base para medir em mm).
            </Item>
            <Item>As réguas nas bordas da imagem mostram a escala em milímetros.</Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'ferramentas',
      titulo: 'As ferramentas',
      busca:
        'ferramentas janela nível contraste brilho mover pan zoom scroll lupa régua medir distância ângulo ROI elíptica área intensidade do pixel comentário seta girar espelhar negativo inverter resetar',
      conteudo: (
        <>
          <P>
            A ferramenta escolhida na barra age com o <strong>botão esquerdo</strong> do mouse. A{' '}
            <strong>rodinha</strong> faz zoom sempre, qualquer que seja a ferramenta.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: (
                  <>
                    <IconeBarra icone={Contrast} /> Janela/Nível
                  </>
                ),
                descricao:
                  'Arrastar muda brilho e contraste. É a ferramenta que já vem ligada ao abrir.',
              },
              { termo: <><IconeBarra icone={Hand} /> Mover</>, descricao: 'Arrasta a imagem dentro do quadro.' },
              { termo: <><IconeBarra icone={ZoomIn} /> Zoom</>, descricao: 'Zoom arrastando (além da rodinha).' },
              {
                termo: <><IconeBarra icone={ScanSearch} /> Lupa</>,
                descricao: 'Amplia só o pedaço sob o cursor enquanto o botão está apertado.',
              },
              { termo: <><IconeBarra icone={Ruler} /> Régua</>, descricao: 'Mede distância em mm entre dois pontos.' },
              { termo: <><IconeBarra icone={Spline} /> Ângulo</>, descricao: 'Mede o ângulo entre três pontos.' },
              {
                termo: <><IconeBarra icone={CircleDashed} /> ROI elíptica</>,
                descricao: 'Desenha uma elipse e mostra área e estatísticas da região.',
              },
              {
                termo: <><IconeBarra icone={Crosshair} /> Intensidade do pixel</>,
                descricao: 'Mostra o valor do pixel no ponto clicado.',
              },
              {
                termo: <><IconeBarra icone={MessageSquarePlus} /> Comentário</>,
                descricao:
                  'Desenha uma seta e pede um texto. Deixar o texto em branco cancela a seta.',
              },
            ]}
          />
          <Sub>Transformações — agem só no quadro em foco</Sub>
          <P>
            <IconeBarra icone={RotateCcw} /> girar 90° (nos dois sentidos),{' '}
            <IconeBarra icone={FlipHorizontal2} /> espelhar (horizontal e vertical),{' '}
            <IconeBarra icone={SunMoon} /> negativo (fica aceso enquanto ligado) e{' '}
            <IconeBarra icone={Maximize2} /> resetar, que devolve o quadro ao zoom, posição e
            contraste originais. Nada disso muda o arquivo: é só a forma de ver.
          </P>
        </>
      ),
    },
    {
      id: 'anotacoes',
      titulo: 'Anotações: excluir, salvar e histórico',
      busca:
        'anotações marcas medidas excluir marca delete lixeira salvar versão comentário da versão histórico carregar versão anterior v1 v2 quem salvou',
      conteudo: (
        <>
          <P>
            Medidas, ângulos, ROIs e comentários são <strong>anotações</strong>. Para apagar uma,
            clique sobre ela (fica selecionada) e aperte <kbd>Del</kbd> ou a lixeira{' '}
            <IconeBarra icone={Trash2} />.
          </P>
          <P>
            Anotação desenhada fica só na sua tela até ser <strong>salva</strong>. O botão{' '}
            <IconeBarra icone={Save} /> pede um comentário opcional (“medidas do nódulo”) e grava uma{' '}
            <strong>versão nova</strong> do conjunto de anotações do estudo. No canto direito da barra
            aparece a versão carregada e quem a salvou (<em>v3 · Ana</em>).
          </P>
          <P>
            Ao abrir um estudo, a última versão salva entra sozinha — quem abrir depois vê as mesmas
            marcas. O botão <IconeBarra icone={History} /> lista todas as versões, com autor, data e
            comentário; <BotaoRef variante="outline">Carregar</BotaoRef> põe uma versão antiga na tela.
          </P>
          <Callout tipo="atencao" titulo="Carregar não desfaz nada">
            Carregar uma versão antiga só a mostra. Nenhuma versão é apagada; se a antiga é a que deve
            valer, salve de novo — ela vira a versão mais nova.
          </Callout>
        </>
      ),
    },
    {
      id: 'casos-chatos',
      titulo: 'Casos chatos',
      busca:
        'imagem em branco preta ilegível duplo clique miniatura recriar sem compressão cache problema na origem nome diferente exame no paciente errado janela bloqueada',
      conteudo: (
        <>
          <Sub>A imagem abre em branco, preta ou embaralhada</Sub>
          <P>
            Dê <strong>duplo‑clique na miniatura</strong> dela. O sistema limpa a cópia guardada e
            busca a imagem de novo, sem compressão (avisa <em>Imagem recriada sem compressão</em>). Se
            mesmo assim falhar, o aviso diz que o problema está na origem — no PACS ou no aparelho — e
            é caso de ticket.
          </P>
          <Sub>O nome da imagem não bate com o do pedido</Sub>
          <P>
            A linha mostra <em>DICOM: …</em> embaixo do nome. Confira se o exame foi associado ao
            pedido certo antes de laudar; se não foi, desassocie (enquanto o laudo não estiver
            assinado) e associe ao pedido correto.
          </P>
          <Sub>O exame não aparece na lista</Sub>
          <Lista>
            <Item>Confira o período e, principalmente, o filtro de tipo de exame (ver acima).</Item>
            <Item>Aumente o limite ou passe para a próxima página.</Item>
            <Item>
              Se aparecer o aviso de varredura truncada, estreite o período: a busca parou antes de
              percorrer tudo.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão módulo PACS consulta edição exclusão laudos inclusão perfil',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'PACS — Consulta',
              descricao: 'Ver a lista e abrir o visualizador, incluindo o histórico de anotações.',
            },
            {
              termo: 'PACS — Edição',
              descricao: 'Associar, desassociar, Resincronizar e salvar anotações.',
            },
            { termo: 'PACS — Exclusão', descricao: 'Excluir exame do PACS.' },
            {
              termo: 'Laudos — Inclusão / Edição',
              descricao: 'Criar o laudo pela linha do exame / abrir o laudo que já existe.',
            },
          ]}
        />
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'dúvidas CID não aparece medida errada laudar apagado anotação sumiu',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Por que o CID não aparece no alto do visualizador?',
              descricao:
                'O exame não está ligado a um pedido, ou o pedido não trouxe CID. Associe o exame e abra o visualizador de novo.',
            },
            {
              termo: 'Por que o ícone de criar laudo está apagado?',
              descricao:
                'O pedido ainda não tem anamnese. Peça para a recepção preencher; ou, se a regra local permitir, ajuste em Configuração de Laudo.',
            },
            {
              termo: 'Minhas medidas sumiram ao reabrir.',
              descricao:
                'Elas não foram salvas. Só o que passa pelo botão Salvar volta na próxima abertura.',
            },
            {
              termo: 'A medida em mm é confiável?',
              descricao:
                'Com calibração “equipamento”, sim. Com “estimada”, use como aproximação; com “ausente”, não use a medida em mm.',
            },
          ]}
        />
      ),
    },
  ],
};
