import { Route } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo dos Translados: as rotas diárias do transporte de pacientes — montar à mão ou gerar, marcar
 * no mapa os endereços que o Google não achou, a rota no tablet do veículo e a viagem que ele
 * registra (o que conta é o transporte feito; o painel corrige).
 *
 * Conferido no código: `features/translados` (TransladosPage — "Gerar automático"/"Novo translado";
 * GerarTransladoPage — Data, "Usar IA", Pré-visualizar/Gerar e salvar, não alocados e
 * RevisaoEnderecos; TransladoDetalhePage — Editar/Iniciar/Cancelar/Concluir, mapa de assentos,
 * sessões elegíveis, ViagemDoTablet e ResgatesDisponiveis), `features/tratamentos` (TratamentoDetalhePage `?sessao=`,
 * PainelConfirmacao) e o backend (`GeradorDeTransladoService` — troca só a rota gerada ainda
 * planejada e replaneja quem estava nela; `RotaTabletService`; `ViagemService` — desembarque =
 * realizada + faturamento, ausência = não realizada, correção do painel vale mais;
 * `FaturamentoService` — cancelado sai dos totais; `GeoController` — permissão de Unidades;
 * `ResgateService` + `LotacaoDaRota` — lista de resgate, aviso "no local", marcação só se couber agora;
 * `TransladoService.CriarAlocacaoAsync` — acompanhantes ganham lugar, condição do paciente).
 */
export const artigoTranslados: Artigo = {
  slug: 'translados',
  titulo: 'Translados (rotas do dia)',
  resumo:
    'As rotas diárias do transporte de pacientes: gerar ou montar, marcar no mapa os endereços que faltam, a rota no tablet do veículo e a viagem que ele registra.',
  grupo: 'transporte',
  icone: Route,
  rota: '/app/translados',
  publico: 'Quem organiza e acompanha o transporte de pacientes (central)',
  atualizadoEm: '2026-10-19',
  palavrasChave: [
    'translado',
    'rota',
    'rota do dia',
    'TFD',
    'transporte',
    'gerar',
    'gerar automático',
    'pré-visualizar',
    'gerar e salvar',
    'não alocado',
    'sem geolocalização',
    'endereço',
    'mapa',
    'marcar no mapa',
    'pino',
    'assento',
    'alocação',
    'tablet',
    'motorista',
    'embarque',
    'embarcou',
    'ausente',
    'faltou',
    'desembarque',
    'desceu',
    'viagem',
    'realizada',
    'não realizada',
    'corrigir',
    'faturamento',
    'em andamento',
    'concluir',
    'cadeirante',
    'maca',
    'ambulância',
    'isolamento',
    'veículo exclusivo',
    'veículo alto',
    'resgate',
    'pacientes disponíveis para resgate',
    'disponível em',
    'aguardando no local',
    'avisar que está no local',
    'marcar para resgate',
    'desmarcar',
    'lugares livres',
    'já está disponível para retorno',
    'ainda não terminei',
    'aguardando há',
    'tempo aguardando',
    'lotação',
    'volta',
    'retorno',
    'fora do município',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'rota diária veículo motorista pacientes do dia coleta destino ordem tablet',
      conteudo: (
        <>
          <P>
            Um <strong>translado</strong> é a rota de um veículo num dia: quem ele busca, em que
            ordem e para onde leva. Ela nasce dos <strong>Atendimentos</strong> (quem viaja, em que
            dias, com que condição e acompanhantes), das <strong>Unidades de Atendimento</strong> (o
            destino) e dos <strong>Tipos de tratamento</strong> (o tempo médio).
          </P>
          <P>
            Depois de salva, a rota aparece sozinha no <strong>tablet do veículo</strong>: o
            motorista vê as paradas na ordem, com o endereço, o ponto no mapa e o que precisa saber
            de cada paciente — e registra a viagem, que volta para cá.
          </P>
        </>
      ),
    },
    {
      id: 'gerar',
      titulo: 'Gerar as rotas do dia',
      busca: 'gerar automático pré-visualizar gerar e salvar data usar ia distribuir veículos ordem de coleta elegíveis cadeirante assento de cadeirante maca ambulância isolamento exclusivo veículo alto carro acompanhantes banco',
      conteudo: (
        <>
          <Passos
            itens={[
              { titulo: <>Em Translados, clique em <BotaoRef variante="outline">Gerar automático</BotaoRef>.</> },
              { titulo: 'Escolha a data.', detalhe: 'Entram as viagens pendentes ou confirmadas até esse dia que ainda não estão numa rota.' },
              {
                titulo: <><BotaoRef variante="outline">Pré-visualizar</BotaoRef> mostra as rotas sem gravar nada.</>,
                detalhe: 'Confira os veículos, a ordem de coleta e a lista de não alocados.',
              },
              {
                titulo: <><BotaoRef>Gerar e salvar</BotaoRef> grava as rotas.</>,
                detalhe: 'A partir daí elas aparecem no tablet de cada veículo.',
              },
            ]}
          />
          <Callout tipo="regra" titulo="Gerar e salvar de novo">
            Só a rota gerada que ainda está <strong>planejada</strong> é trocada — e quem estava nela é
            replanejado junto. Rota <strong>em andamento</strong> (o carro está na rua, com o tablet
            seguindo ela), <strong>concluída</strong> ou <strong>cancelada</strong> fica como está.
          </Callout>
          <Sub>Como o veículo e o assento são escolhidos</Sub>
          <Lista>
            <Item>Cadeirante que viaja na própria cadeira vai no <strong>assento de cadeirante</strong>.</Item>
            <Item>Quem vai de <strong>maca</strong>, só em <strong>ambulância</strong> — uma por veículo.</Item>
            <Item>Imunodeficiente (isolamento) vai em <strong>veículo exclusivo</strong>, só com os acompanhantes dele.</Item>
            <Item>Cada <strong>acompanhante</strong> da viagem ganha um banco (até 2).</Item>
            <Item>
              Quem não sobe em veículo alto vai de <strong>carro</strong> quando houver um livre. A
              ambulância e o veículo adaptado ficam, de preferência, para quem precisa deles.
            </Item>
          </Lista>
          <P>
            Quem mais exige escolhe primeiro (maca, isolamento, cadeirante, acompanhantes), e cada
            veículo é completado com quem couber.
          </P>
          <P>
            “Usar IA” pede a uma IA para distribuir os pacientes nos veículos; o sistema confere a
            sugestão pelas mesmas regras e, se ela não servir, distribui sozinho.
          </P>
        </>
      ),
    },
    {
      id: 'nao-alocados',
      titulo: 'Não alocados e endereços para marcar no mapa',
      busca: 'não alocados sem geolocalização revisar endereço marcar no mapa salvar ponto latitude longitude unidade sem coordenada precisa de ambulância assento de cadeirante acompanhantes',
      conteudo: (
        <>
          <P>
            Quem não coube numa rota aparece em <strong>não alocados</strong>, com o motivo. Os dois
            mais comuns:
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Sem geolocalização do paciente',
                descricao: 'O Google não achou o endereço (ou só achou a rua ou o bairro). Sem ponto no mapa, a rota não sabe onde buscar.',
              },
              {
                termo: 'Unidade de atendimento sem coordenada',
                descricao: 'O destino está sem o ponto no mapa — corrija no cadastro da unidade.',
              },
              {
                termo: 'Precisa de ambulância / de assento de cadeirante / lugar para os acompanhantes',
                descricao: 'Não sobrou veículo livre que atenda a condição do paciente. Libere um veículo, monte a rota à mão ou confira o cadastro do atendimento.',
              },
            ]}
          />
          <Sub>Marcar o endereço no mapa</Sub>
          <Passos
            itens={[
              { titulo: <>Em “Endereços para marcar no mapa”, clique em <BotaoRef variante="outline">Marcar no mapa</BotaoRef>.</> },
              { titulo: 'Clique no ponto certo do mapa — ou cole “latitude, longitude”.' },
              { titulo: <><BotaoRef>Salvar ponto</BotaoRef>.</> },
              { titulo: <>Clique em <BotaoRef variante="outline">Pré-visualizar</BotaoRef> de novo: o paciente entra na rota.</> },
            ]}
          />
          <Callout tipo="dica" titulo="Vale para as próximas vezes">
            O ponto fica guardado para aquele endereço: na próxima geração, o paciente já entra sem
            precisar marcar de novo.
          </Callout>
        </>
      ),
    },
    {
      id: 'montar',
      titulo: 'Montar ou ajustar uma rota à mão',
      busca: 'novo translado data veículo motorista mapa de assentos sessões elegíveis alocar desalocar editar iniciar cancelar acompanhante sem lugar não cabe lotação cadeirante',
      conteudo: (
        <>
          <P>
            <BotaoRef>Novo translado</BotaoRef> cria uma rota vazia (data, veículo, motorista). No
            detalhe, enquanto a rota está <SeloRef cor="info">Planejada</SeloRef>, clique numa sessão
            elegível e depois num assento livre — ou no assento primeiro. Clicar num assento ocupado
            oferece desalocar: o paciente sai da rota com os acompanhantes.
          </P>
          <Callout tipo="atencao" titulo="Nunca mais gente do que cabe">
            O paciente vai no assento que você escolheu, e cada acompanhante da viagem ganha outro lugar
            livre sozinho. Sem lugar para todos, o sistema não aloca e diz quantos lugares sobram.
            Cadeirante na própria cadeira só vai no assento de cadeirante, e maca só de ambulância. Isolamento
            não divide o carro. Trocar os acompanhantes de quem já está na rota segue a mesma regra.
          </Callout>
          <P>
            Se o paciente confirmar mais um acompanhante pelo WhatsApp com o carro cheio, a rota mostra
            em vermelho “acompanhante sem lugar no carro”. Libere um lugar ou troque o paciente de rota.
          </P>
          <Lista>
            <Item>
              <BotaoRef variante="outline">Editar</BotaoRef> troca data, veículo ou motorista.
            </Item>
            <Item>
              <BotaoRef>Iniciar</BotaoRef> e <BotaoRef>Concluir</BotaoRef> existem aqui, mas o normal é
              o tablet fazer isso (abaixo). Concluir devolve à lista de resgate quem a rota ainda não buscou.
            </Item>
            <Item>
              <BotaoRef variante="danger">Cancelar</BotaoRef> desfaz a rota: as viagens voltam a ser
              elegíveis para outra.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'tablet',
      titulo: 'A rota no tablet e a viagem que ele registra',
      busca: 'tablet motorista rosto iniciar navegação embarcou ausente desembarcou desceu encerrar a rota concluída sem rede fila em andamento',
      conteudo: (
        <>
          <P>
            O motorista entra no tablet pelo rosto e abre a navegação; nessa hora a rota passa a{' '}
            <SeloRef cor="alerta">Em andamento</SeloRef>. No embarque ele marca, de cada pessoa
            (paciente e cada acompanhante), <strong>Embarcou</strong> ou <strong>Ausente</strong>. No
            desembarque só existe <strong>Desembarcou</strong>, e só para quem embarcou.
          </P>
          <P>
            Ao desembarcar na unidade, o tablet abre a lista de resgate (acima), e o carro não segue
            sozinho para lugar nenhum. Sem paradas pendentes, o motorista resgata mais alguém ou toca em{' '}
            <BotaoRef>Encerrar a rota</BotaoRef>, e a rota fica <SeloRef cor="sucesso">Concluída</SeloRef>.
          </P>
          <P>
            No detalhe do translado, <strong>Viagem registrada pelo tablet</strong> mostra tudo na
            ordem, com a hora, e a situação de cada paciente. Com a rota em andamento, a lista se
            atualiza sozinha.
          </P>
          <Callout tipo="atencao" titulo="Sem sinal no caminho">
            O tablet guarda as marcações e manda quando a rede volta. A hora que aparece é a da
            marcação, não a da chegada aqui.
          </Callout>
        </>
      ),
    },
    {
      id: 'resgate',
      titulo: 'O resgate: buscar na unidade e deixar em casa',
      busca: 'resgate retorno volta pacientes disponíveis para resgate disponível em minutos aguardando no local avisar paciente central motorista marcar desmarcar outro carro lugares livres fora do município previsão tempo médio buscar na unidade entregar em casa você já está disponível para retorno ainda não terminei procedimento pergunta 10 minutos',
      conteudo: (
        <>
          <P>
            O <strong>resgate</strong> é buscar o paciente na unidade depois do atendimento e deixá-lo em
            casa. Ninguém entra nele sozinho, nem fora do município: alguém escolhe quem cada carro vai
            buscar.
          </P>
          <Sub>Pacientes disponíveis para resgate</Sub>
          <P>
            Quem desce na unidade entra logo na lista <strong>Pacientes disponíveis para resgate</strong>,
            como “Disponível em XX min”: a hora que a central marcou na viagem ou, sem ela, a chegada
            mais o tempo médio do procedimento. A lista é <strong>a mesma para todos</strong>: os tablets
            de todos os carros e o detalhe de qualquer translado planejado ou em andamento.
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Aguardando no local', descricao: 'Avisaram que ele já terminou e está esperando: vem primeiro.' },
              { termo: 'Disponível pela previsão', descricao: 'Ninguém avisou, mas o tempo do procedimento já passou.' },
              { termo: 'Disponível em XX min', descricao: 'Ainda em atendimento, pela previsão.' },
              { termo: 'Sem previsão', descricao: 'Sem hora marcada nem tempo médio: só o aviso diz quando.' },
            ]}
          />
          <Sub>Quem avisa que o paciente está no local</Sub>
          <Lista>
            <Item>
              o próprio <strong>paciente</strong>, pelo app. Quando a previsão estoura, o app pergunta “Você já está
              disponível para retorno?”. <strong>“Sim, já estou disponível”</strong> põe o paciente no topo da
              lista. <strong>“Não, ainda não terminei o procedimento”</strong> faz a pergunta voltar em 10
              minutos, até ele dizer que sim; enquanto isso, a lista mostra “Disponível em XX min” até a
              próxima pergunta. Quem terminou antes do previsto avisa sozinho, a qualquer hora (“Já
              terminei, estou disponível para retorno”);
            </Item>
            <Item>
              a <strong>central</strong>, aqui: <BotaoRef variante="outline">Avisar que está no local</BotaoRef>{' '}
              (por exemplo, depois de uma ligação da clínica);
            </Item>
            <Item>um <strong>motorista</strong> que chegou ao local e viu o paciente esperando.</Item>
          </Lista>
          <Sub>Marcar quem o carro vai buscar</Sub>
          <P>
            Cada linha mostra o paciente, a unidade e há quanto tempo ele espera (“Aguardando há 25
            min”); quem espera há mais tempo vem primeiro. Qualquer carro resgata qualquer paciente da
            lista, mesmo que não o tenha levado. O motorista
            marca no tablet, e a central marca aqui: escolha os pacientes e clique em{' '}
            <BotaoRef>Marcar para resgate nesta rota</BotaoRef>. Quem marcar primeiro fica com o
            paciente, e ele sai da lista dos outros. O tablet passa a mostrar “Resgate — unidade” e, depois
            das buscas, a entrega na casa de cada um. Em <strong>Marcados para esta rota</strong>,{' '}
            <BotaoRef variante="outline">Desmarcar</BotaoRef> devolve o paciente à lista, se ele ainda não
            embarcou.
          </P>
          <Callout tipo="atencao" titulo="Só se couber no carro agora">
            Vale quem está no carro <strong>naquela hora</strong>. Quem foi na ida libera o lugar ao descer
            na unidade, e quem foi marcado ocupa o lugar até chegar em casa. Cada acompanhante leva um
            lugar, e a condição do paciente conta: cadeirante, maca ou isolamento. A lista mostra os lugares
            livres agora e, em vermelho, por que um paciente não cabe.
          </Callout>
          <P>
            Quando o motorista não encontra o paciente no local, o paciente <strong>volta para a lista</strong>,
            com o aviso “Não estava no local às HH:MM”, e o aviso de que estava esperando cai. Se a rota
            for concluída sem buscar alguém marcado, esse paciente também volta para a lista.
          </P>
        </>
      ),
    },
    {
      id: 'o-que-conta',
      titulo: 'O que conta: o transporte feito',
      busca: 'realizada não realizada transporte feito atendimento faturamento bpa contabiliza translado desembarque ausência',
      conteudo: (
        <>
          <P>
            O que se contabiliza é o <strong>translado</strong> — o paciente foi levado — e não o
            atendimento lá dentro: ele pode ter sido levado e, por outro motivo, não ter sido atendido.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Desceu no destino',
                descricao: 'A viagem fica realizada, com o motorista, o veículo e as horas de saída e de chegada — e entra no Faturamento.',
              },
              {
                termo: 'Não estava na coleta',
                descricao: 'A viagem fica não realizada (“Paciente não estava na coleta (registrado no tablet)”) e não entra no Faturamento.',
              },
            ]}
          />
          <P>
            Acompanhante embarcando ou faltando fica registrado, mas não muda a viagem do paciente. O
            resgate não muda a contagem: o translado já foi contado na ida; ele só completa a saída da
            unidade e a chegada em casa, com o motorista e o carro que buscaram.
          </P>
        </>
      ),
    },
    {
      id: 'corrigir',
      titulo: 'Corrigir o que o tablet registrou',
      busca: 'corrigir confirmar realização atendimento painel vale mais tablet não muda faturamento cancelado volta a contar',
      conteudo: (
        <>
          <P>
            Em “Viagem registrada pelo tablet”, <BotaoRef variante="ghost">Corrigir</BotaoRef> abre a
            viagem no Atendimento do paciente, já com o que o tablet registrou. Ajuste e confirme.
          </P>
          <Lista>
            <Item>
              Corrigida pela central, <strong>vale o que a central confirmou</strong>: o paciente
              aparece como “Corrigido pela central”, e marcação do tablet que chegar depois fica na
              lista com o aviso “Não mudou a viagem”.
            </Item>
            <Item>
              Marcou <strong>não realizada</strong>: sai dos totais do Faturamento. Corrigiu de volta
              para realizada: volta a contar.
            </Item>
          </Lista>
          <Callout tipo="regra" titulo="Já no BPA">
            Se o translado já foi para o BPA ou foi faturado, o sistema não deixa dizer que ele não
            aconteceu: é preciso tirá-lo do faturamento antes.
          </Callout>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão perfil translados consulta inclusão edição unidades marcar no mapa atendimentos corrigir resgate',
      conteudo: (
        <ListaDefinicoes
          itens={[
            { termo: 'Translados — Consulta', descricao: 'Ver as rotas e a viagem registrada pelo tablet.' },
            { termo: 'Translados — Inclusão', descricao: 'Gerar automático e gravar as rotas, ou criar uma à mão.' },
            { termo: 'Translados — Edição', descricao: 'Alocar e desalocar, iniciar, concluir; marcar e desmarcar o resgate, avisar que o paciente está no local.' },
            { termo: 'Unidades — Edição', descricao: 'Marcar no mapa os endereços que o Google não achou.' },
            { termo: 'Atendimentos — Edição', descricao: 'Corrigir a viagem (o botão Corrigir).' },
          ]}
        />
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'tablet não mostra rota nenhum translado hoje paciente sumiu da rota gerar de novo marquei errado',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'O tablet diz “Nenhum translado hoje para este veículo”.',
              descricao: 'Não há rota salva para esse veículo hoje. Gere e salve (ou monte à mão) — o tablet atualiza puxando a tela para baixo.',
            },
            {
              termo: 'Gerei de novo: o carro que já saiu perdeu a rota?',
              descricao: 'Não. Rota em andamento não é trocada; só as planejadas.',
            },
            {
              termo: 'O motorista marcou errado no tablet.',
              descricao: 'Use Corrigir na viagem do translado. Depois da correção, o tablet não muda mais aquela viagem.',
            },
          ]}
        />
      ),
    },
  ],
};
