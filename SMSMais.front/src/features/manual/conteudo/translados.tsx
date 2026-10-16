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
 * sessões elegíveis e ViagemDoTablet), `features/tratamentos` (TratamentoDetalhePage `?sessao=`,
 * PainelConfirmacao) e o backend (`GeradorDeTransladoService` — troca só a rota gerada ainda
 * planejada e replaneja quem estava nela; `RotaTabletService`; `ViagemService` — desembarque =
 * realizada + faturamento, ausência = não realizada, correção do painel vale mais;
 * `FaturamentoService` — cancelado sai dos totais; `GeoController` — permissão de Unidades).
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
  atualizadoEm: '2026-10-16',
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
    'volta',
    'retorno',
    'espera',
    'puxar',
    'aguardando retorno',
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
      busca: 'novo translado data veículo motorista mapa de assentos sessões elegíveis alocar desalocar editar iniciar cancelar',
      conteudo: (
        <>
          <P>
            <BotaoRef>Novo translado</BotaoRef> cria uma rota vazia (data, veículo, motorista). No
            detalhe, enquanto a rota está <SeloRef cor="info">Planejada</SeloRef>, clique numa sessão
            elegível e depois num assento livre — ou no assento primeiro. Clicar num assento ocupado
            oferece desalocar.
          </P>
          <Lista>
            <Item>
              <BotaoRef variante="outline">Editar</BotaoRef> troca data, veículo ou motorista.
            </Item>
            <Item>
              <BotaoRef>Iniciar</BotaoRef> e <BotaoRef>Concluir</BotaoRef> existem aqui, mas o normal é
              o tablet fazer isso sozinho (abaixo).
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
      busca: 'tablet motorista rosto iniciar navegação embarcou ausente desceu concluída sem rede fila em andamento',
      conteudo: (
        <>
          <P>
            O motorista entra no tablet pelo rosto e abre a navegação; nessa hora a rota passa a{' '}
            <SeloRef cor="alerta">Em andamento</SeloRef>. Em cada parada ele marca quem{' '}
            <strong>embarcou</strong> e quem <strong>não estava</strong> (paciente e cada
            acompanhante); no destino, quem <strong>desceu</strong>. Resolvida a última parada, a rota
            fica <SeloRef cor="sucesso">Concluída</SeloRef>.
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
      id: 'volta',
      titulo: 'A volta: depende do destino',
      busca: 'volta retorno espera fora do município maricá puxar aguardando a volta pronto tempo médio buscar na unidade entregar em casa',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Fora do município',
                descricao: (
                  <>
                    A van <strong>espera</strong>. No tablet, depois do destino, vem “Volta — unidade”, com
                    a hora prevista (chegada + tempo médio do tratamento), e depois a entrega na casa de
                    cada um. Quem faltou na ida não entra na volta.
                  </>
                ),
              },
              {
                termo: 'Em Maricá',
                descricao: (
                  <>
                    A van <strong>não espera</strong>: a rota acaba no destino. Quem desceu aparece em{' '}
                    <strong>Aguardando a volta</strong>, no detalhe de qualquer translado planejado ou em
                    andamento, com a hora em que deve estar pronto. <BotaoRef variante="outline">Puxar para esta rota</BotaoRef>{' '}
                    põe a volta na rota: o tablet passa a mostrar a busca na unidade e a entrega em casa.
                  </>
                ),
              },
            ]}
          />
          <Callout tipo="dica" titulo="Van cheia">
            O lugar segue a condição do paciente e os acompanhantes, como na geração. Sem lugar na rota,
            o sistema avisa: puxe para outra, ou crie um translado só para a volta (Novo translado, mesmo
            veículo) e puxe para ele.
          </Callout>
          <P>
            Quem não estava na unidade quando o carro saiu fica <strong>aguardando a volta</strong> e
            aparece na lista para ser puxado — mesmo fora do município.
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
            Acompanhante embarcando ou faltando fica registrado, mas não muda a viagem do paciente. A volta
            não muda a contagem: o translado já foi contado na ida; ela só completa a saída da unidade e a
            chegada em casa.
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
      busca: 'permissão perfil translados consulta inclusão edição unidades marcar no mapa atendimentos corrigir',
      conteudo: (
        <ListaDefinicoes
          itens={[
            { termo: 'Translados — Consulta', descricao: 'Ver as rotas e a viagem registrada pelo tablet.' },
            { termo: 'Translados — Inclusão', descricao: 'Gerar automático e gravar as rotas, ou criar uma à mão.' },
            { termo: 'Translados — Edição', descricao: 'Alocar e desalocar, iniciar, concluir.' },
            { termo: 'Unidades — Edição', descricao: 'Marcar no mapa os endereços que o Google não achou.' },
            { termo: 'Atendimentos — Edição', descricao: 'Corrigir a viagem (o botão Corrigir).' },
            { termo: 'Rastreamento — Consulta / Edição', descricao: 'Ver quem aguarda a volta / puxar para uma rota.' },
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
