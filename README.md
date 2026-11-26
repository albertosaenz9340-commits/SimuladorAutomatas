# Simulador de Autómatas Finitos Deterministas (AFD)

## 12.1. Información del proyecto

**Título:** Simulador de Autómatas Finitos Deterministas (AFD).

**Integrantes:**

- Alberto Mario Sáenz Molina
- Aaron Levi Medina Medina
- Camilo Andres Maldonado Duarte
- Jhon Ramírez Salgado

### Descripción general

Aplicación de escritorio para Windows que permite construir un autómata finito determinista a partir de su definición formal, visualizarlo como un diagrama dibujado con GDI+, buscar las cinco palabras aceptadas más cortas y comprobar si una palabra concreta pertenece al lenguaje reconocido. El autómata se define como la quíntupla `M = (Q, Σ, δ, q0, F)` con `Q` de exactamente cinco nodos. Los tres módulos comparten una única definición: al construir una nueva se reemplaza la anterior.

### Objetivo general

Desarrollar una aplicación completa, funcional y organizada que implemente correctamente los fundamentos matemáticos de los autómatas finitos deterministas y permita explorar el lenguaje que reconocen.

### Objetivos específicos

- Definir la quíntupla `M = (Q, Σ, δ, q0, F)` con exactamente cinco nodos mediante una interfaz gráfica construida por código.
- Configurar el estado inicial `q0` y el conjunto `F` de estados finales como parte de esa quíntupla.
- Validar que la definición sea un AFD completo, no ambiguo y con cinco estados.
- Mostrar la representación formal, la tabla de transiciones y el diagrama gráfico.
- Encontrar hasta cinco palabras aceptadas de longitud máxima 10, ordenadas por longitud y por el orden del alfabeto.
- Procesar palabras arbitrarias informando del recorrido de estados, del resultado y de los símbolos inválidos.
- Mantener una sola definición compartida entre los tres módulos (quíntupla completa).

### Alcance del sistema

- Construcción, modificación y validación de AFD completos con cinco estados.
- Visualización GDI+ con los cinco estados, finales, flecha inicial, arcos y bucles.
- Búsqueda acotada de palabras aceptadas (longitud ≤ 10, máximo 5 resultados).
- Reconocimiento de palabras sobre el alfabeto definido, incluida la palabra vacía `ε`.
- Cuatro casos de prueba precargados, todos con cinco estados.

No están incluidos: AFD no deterministas, conversiones entre autómatas, operaciones entre lenguajes en la interfaz, persistencia en disco ni exportación de archivos.

### Tecnologías utilizadas

| Concepto | Tecnología |
| --- | --- |
| Lenguaje | C# 12 |
| Framework | .NET 8 (`net8.0-windows`) |
| Interfaz | Windows Forms |
| Gráficos | GDI+ (`System.Drawing`) |
| Entorno | Visual Studio 2022 (carga de trabajo *Desarrollo de escritorio con .NET*) |
| Sistema operativo | Windows 10/11 |
| Dependencias externas | Ningún paquete NuGet |

### Arquitectura y organización del código

Se aplica una separación por responsabilidades sobre programación orientada a objetos:

- **Models:** estructura de datos de la quíntupla `M = (Q, Σ, δ, q0, F)`.
- **Services:** validación, estado compartido, reconocimiento, búsqueda y casos de prueba.
- **Forms:** interfaz de usuario de cada módulo.
- **Controls:** control personalizado de dibujo.
- **UI:** estilos y fábricas de controles comunes.
- **Pruebas:** proyecto de consola con comprobaciones automáticas.

## 12.2. Especificaciones técnicas

| Especificación | Valor real |
| --- | --- |
| Lenguaje de programación | C# 12 |
| Framework | .NET 8, moniker `net8.0-windows` |
| Interfaz gráfica | Windows Forms (`UseWindowsForms=true`) |
| Tecnología de gráficos | GDI+ mediante `System.Drawing` (`Graphics.DrawEllipse`, `DrawBezier`, `FillPolygon`, etc.) |
| Entorno compatible | Visual Studio 2022 con carga de trabajo *Desarrollo de escritorio con .NET* |
| Sistema operativo objetivo | Windows 10/11 |
| Tipo de aplicación | Aplicación de escritorio `WinExe` |
| Patrón de organización | Separación en capas Models / Services / Forms / Controls; servicio central singleton para la definición compartida |
| Bibliotecas | Biblioteca estándar de .NET y Windows Forms; sin paquetes externos |

El equipo de desarrollo verificó la compilación con el SDK instalado en el entorno de trabajo (`10.0.301`), que incluye el tiempo de ejecución y los ensamblados de referencia de Windows Desktop 8.0. El proyecto **compila contra .NET 8** porque su `TargetFramework` es `net8.0-windows`.

### Principales modelos, clases y servicios

| Archivo | Responsabilidad |
| --- | --- |
| `Models/Estado.cs` | Nombre de un estado con igualdad exacta por cadena. |
| `Models/Transicion.cs` | Tripleta `(origen, símbolo, destino)` que representa `δ`. |
| `Models/Automata.cs` | Quíntupla inmutable `M = (Q, Σ, δ, q0, F)` con cinco estados; expone `Estados`, `Alfabeto`, `EstadoInicial`, `EstadosFinales`, `Transiciones`, `IntentarTransitar` y `RepresentacionFormal`. |
| `Services/ValidacionAutomataService.cs` | Valida la quíntupla con exactamente cinco nodos y devuelve `ResultadoValidacion`. |
| `Services/AutomataService.cs` | Singleton con `Actual`, `Establecer(...)`, `Limpiar()` y el evento `AutomataCambiado`. |
| `Services/ReconocimientoService.cs` | Normaliza la entrada, valida símbolos, aplica `δ` desde `q0` y produce `ResultadoReconocimiento`. |
| `Services/BusquedaPalabrasService.cs` | Calcula la búsqueda ordenada de palabras aceptadas desde `q0` con conteo dinámico. |
| `Services/CasosPruebaService.cs` | Cuatro definiciones precargadas y validadas antes de construirse. |
| `Controls/GrafoAutomata.cs` | Panel con dibujo GDI+, desplazamiento por arrastre y zoom con la rueda del ratón. |
| `UI/Estilos.cs` | Paleta, fuentes y creación programática de botones, tablas, rejillas y grupos. |
| `Forms/MenuPrincipal.cs` | Menú con los tres módulos y el estado del autómata actual. |
| `Forms/ConstructorAutomata.cs` | Captura de la definición, validación y visualización de resultados. |
| `Forms/BusquedaPalabras.cs` | Presentación de los resultados del módulo 2. |
| `Forms/ReconocimientoPalabras.cs` | Entrada de palabras y presentación del recorrido y resultado. |

### Forma en que se comparte el autómata

`AutomataService.Instancia` es un singleton al que acceden los cuatro formularios. El constructor valida la quíntupla completa y llama a `Establecer(...)`. Los módulos 2 y 3 leen `AutomataService.Instancia.Actual`, que contiene `q0` y `F` junto con `Q`, `Σ` y `δ`; si no existe, el menú les impide abrirse y los propios formularios muestran un aviso. La lógica de reconocimiento sólo existe en `ReconocimientoService`, no se repite en los formularios.

### Algoritmos utilizados

- **Reconocimiento:** recorrido secuencial de la palabra desde el estado inicial `q0` de la quíntupla. Para cada símbolo se comprueba pertenencia a `Σ`, se aplica `δ` y se añade el estado alcanzado al recorrido. La palabra es aceptada si el último estado pertenece a `F`.
- **Búsqueda:** para cada par `(estado, longitud)` se calcula cuántas palabras aceptadas de esa longitud salen de dicho estado, saturando el conteo en 5. Después se enumeran las ramas útiles en orden de símbolos, de longitud 0 a 10. Es una enumeración ordenada equivalente en su resultado a una BFS por niveles, pero con poda exacta: no genera palabras que no pueden ser aceptadas y no fusiona palabras distintas que llegan al mismo estado.

## 12.3. Fundamentos teóricos

### Alfabetos

Un alfabeto `V` o `Σ` es un conjunto finito no vacío de símbolos. En la aplicación cada símbolo es un carácter individual, por ejemplo `Σ = {0, 1}` o `Σ = {a, b}`.

### Palabras y palabra vacía

Una palabra es una secuencia finita de símbolos de `Σ`. La palabra vacía se denota `ε` (también `λ`) y tiene longitud cero: `|ε| = 0`. El programa admite `ε`, `λ` o un campo vacío para representarla.

### Concatenación de palabras

Si `x = ab` e `y = ba`, entonces `xy = abba`. La concatenación es asociativa y `ε` es su elemento neutro: `εw = wε = w`.

### Clausura de Kleene

`Σ*` es el conjunto de todas las palabras finitas sobre `Σ`, incluida `ε`:

`Σ⁰ = {ε}`, `Σ¹ = Σ`, `Σ² = Σ·Σ`, y `Σ* = Σ⁰ ∪ Σ¹ ∪ Σ² ∪ …`

### Lenguajes formales

Un lenguaje formal `L` sobre `Σ` es cualquier subconjunto de `Σ*`, es decir, `L ⊆ Σ*`. Puede ser finito o infinito. El lenguaje reconocido por un AFD se denota `L(M)`.

### Operaciones con lenguajes

| Operación | Definición |
| --- | --- |
| Concatenación | `L₁·L₂ = { xy ∣ x ∈ L₁, y ∈ L₂ }` |
| Potencia | `L⁰ = {ε}`, `Lⁿ = L·Lⁿ⁻¹` |
| Clausura de Kleene | `L* = L⁰ ∪ L¹ ∪ L² ∪ …` |
| Clausura positiva | `L⁺ = L¹ ∪ L² ∪ …` |
| Reversión | `Lᴿ = { wᴿ ∣ w ∈ L }` |
| Complemento | `Lᶜ = Σ* − L` |

Estas operaciones forman parte de la base teórica del proyecto. La interfaz implementa el reconocimiento del lenguaje de un AFD; no ofrece botones para ejecutar las operaciones de la tabla.

### Monoide libre

El conjunto `Σ*` junto con la concatenación constituye el monoide libre: la operación es asociativa y `ε` es el elemento neutro.

### Autómatas finitos deterministas

En este proyecto un AFD se define mediante la quíntupla exigida en el requerimiento:

`M = (Q, Σ, δ, q0, F)`

donde:

- `Q`: conjunto finito de exactamente cinco estados (nodos).
- `Σ`: alfabeto finito de entrada.
- `δ: Q × Σ → Q`: función de transición total.
- `q0 ∈ Q`: estado inicial.
- `F ⊆ Q`: conjunto de estados finales.

Los cinco componentes se introducen y validan juntos en el Módulo 1 y se muestran en la pestaña *Representación formal*. `δ` describe la estructura del autómata, mientras que `q0` y `F` determinan el lenguaje que se reconoce.

### Función de transición

`δ(q, a)` indica el único estado al que se llega desde `q` al leer `a`. Por ser una función completa debe existir exactamente una transición para cada par estado-símbolo, es decir `|Q| × |Σ|` transiciones. No se permiten transiciones duplicadas ni destinos inexistentes.

### Estado inicial y estados finales

El estado inicial `q0` es donde comienza el procesamiento y es el cuarto componente de `M`. Los estados de `F` se dibujan con relleno verde sólido y texto blanco, y se marcan con `*` en la tabla. El inicial se marca con `→`.

### Criterio de aceptación y rechazo

Al consumir todos los símbolos de `w` desde `q0`, se obtiene un estado alcanzado. Si ese estado pertenece a `F`, la palabra es **aceptada**; en caso contrario es **rechazada**. La palabra vacía `ε` es aceptada si y sólo si `q0 ∈ F`.

## 12.4. Explicación de los módulos

### Módulo 1. Construcción de autómatas finitos

**Propósito.** Definir y validar la quíntupla `M = (Q, Σ, δ, q0, F)` con cinco nodos para que la utilicen los otros dos módulos.

**Entradas.** Exactamente cinco estados (separados por comas, punto y coma o espacios), símbolos del alfabeto (cada carácter distinto de separador se trata como un símbolo), el estado inicial seleccionable, los estados finales marcados con casillas y las transiciones forman la quíntupla. Las transiciones se pueden agregar con los controles `Origen`, `Símbolo` y `Destino`, editar directamente en las columnas desplegables de la rejilla y quitar con el botón correspondiente. También se pueden agregar o quitar estados y símbolos en cualquier momento, siempre que `Q` quede con cinco nodos.

**Procesamiento.** El botón **VALIDAR Y CONSTRUIR** comprueba que:

- `Q` tenga exactamente cinco estados y `Σ` no esté vacío.
- No haya estados ni símbolos duplicados.
- Los orígenes, destinos y símbolos de las transiciones existan.
- No haya transiciones duplicadas.
- Existan las `|Q| × |Σ|` transiciones.
- `q0` pertenezca a `Q`.
- Todo estado final pertenezca a `Q`.

**Resultados.** Pestaña *Representación formal* con la quíntupla `M = (Q, Σ, δ, q0, F)` completa, sus cinco componentes por separado y la lista de `δ`; pestaña *Tabla de transiciones* con estados en filas, símbolos en columnas, `→` para el inicial y `*` para los finales; pestaña *Diagrama (GDI+)* con título centrado *Autómata finito determinista*, cinco nodos en pentágono irregular (azul pastel `#DCEAF8`, contorno `#34495E`) con `q0` en el flanco derecho, estados finales verdes con doble circunferencia y texto blanco, indicador azul *Inicio* situado encima de una flecha que sale hacia la derecha desde `q0` (ancho medido del texto para no recortarlo), transiciones como arcos Bézier compactos con puntas triangulares tangentes al perímetro destino, bucles exteriores pequeños y etiquetas ancladas al punto medio de cada curva, agrupadas por origen-destino y colocadas evitando colisiones con nodos, botón y otras etiquetas; la zona derecha de `q0` queda reservada en exclusiva para *Inicio*, sin que bucles ni transiciones la invadan. Conserva el desplazamiento arrastrando y el zoom uniforme con la rueda. La captura real del caso de cinco estados, renderizada con `DrawToBitmap`, se incluye en `Documentacion - Ejemplos de entrada/Diagrama_verificado.png`. La vista previa se actualiza cuando cambia la definición. El cuadro *Casos de prueba* permite cargar los cuatro ejemplos incluidos.

### Módulo 2. Búsqueda de las cinco palabras más cortas

**Propósito.** Encontrar hasta cinco palabras de `L(M)` con longitud máxima 10.

**Funcionamiento.** El servicio calcula cuántas palabras aceptadas de cada longitud existen desde cada estado. Con esa información recorre únicamente las ramas que pueden terminar en un estado final, en orden de longitud ascendente y, dentro de una misma longitud, en el orden en que el usuario introdujo los símbolos de `Σ`.

**Resultados.** Una tabla con número de orden, palabra (`ε` cuando corresponde), longitud, recorrido completo de estados y estado final. Si hay menos de cinco palabras se muestran todas. Si no hay ninguna, se indica explícitamente que no se encontraron palabras aceptadas con longitud ≤ 10.

### Módulo 3. Reconocimiento de palabras

**Propósito.** Comprobar si una palabra pertenece a `L(M)`.

**Entradas y procesamiento.** Se escribe la palabra y se pulsa **PROCESAR PALABRA** (también funciona la tecla Enter). El servicio comprueba cada símbolo; si encuentra uno fuera de `Σ`, informa del carácter y de su posición empezando en 1. Si todos son válidos, aplica `δ` y registra el recorrido.

**Resultados.** Cuadros con símbolos procesados, recorrido `q0 → … → qf`, estado final y resultado `ACEPTADA`, `RECHAZADA` o `ENTRADA INVÁLIDA`. La palabra vacía se procesa sin leer símbolos y el recorrido es únicamente `q0`. Se pueden realizar todas las pruebas necesarias sin reconstruir el autómata.

## 12.5. Tutorial de instalación y puesta en marcha

### 1. Instalar el SDK de .NET 8

Descargue el SDK de .NET 8 desde <https://dotnet.microsoft.com/download> y ejecute el instalador. Compruebe la instalación:

```powershell
dotnet --list-sdks
```

Para desarrollar Windows Forms se necesita además el componente *Windows Desktop* incluido en el SDK de Windows. En el entorno donde se verificó este proyecto había instalados el SDK `10.0.301` y los componentes `Microsoft.WindowsDesktop.App` 8.0.x; la compilación se realiza contra .NET 8.

### 2. Instalar Visual Studio 2022

Descargue **Visual Studio 2022 Community** desde <https://visualstudio.microsoft.com/> y, durante la instalación, seleccione la carga de trabajo **Desarrollo de escritorio con .NET** (*.NET desktop development*). Esa carga de trabajo incluye Windows Forms, el diseñador y el SDK de .NET necesario; no hacen falta extensiones adicionales para GDI+ ni paquetes NuGet.

### 3. Abrir el proyecto

En Visual Studio: *Archivo → Abrir → Proyecto o solución…* y seleccione directamente:

```
C:\Users\alber\OneDrive\Documentos\Estudio y Trabajo\Experimental\SimuladorAutomatas\SimuladorAutomatas.csproj
```

El proyecto **no tiene archivo `.sln` ni `.slnx`**: se abre el `.csproj` directamente. Tampoco hay archivos `.Designer.cs`: toda la interfaz se construye por código.

### 4. Restaurar y compilar

Visual Studio restaura y compila automáticamente. Para hacerlo manualmente: *Generar → Restaurar paquetes NuGet* y después *Generar → Compilar solución* (`Ctrl + Mayús + B`).

Resultado verificado: **0 errores, 0 advertencias**.

### 5. Ejecutar

Asegúrese de que **SimuladorAutomatas** es el proyecto de inicio y pulse **F5** (depurar) o **Ctrl + F5** (sin depurar). También puede ejecutar directamente:

```
bin\Debug\net8.0-windows\SimuladorAutomatas.exe
```

### 6. Ejecutar las pruebas automáticas

En la ventana *Consola del administrador de paquetes* o en una terminal de PowerShell situada en la carpeta del proyecto:

```powershell
dotnet run --project Pruebas/Pruebas.csproj
```

Resultado verificado: **55 comprobaciones, 0 fallos**.

### 7. Solución de problemas habituales

| Problema | Acción |
| --- | --- |
| `dotnet` no se reconoce | Cierre y reabra la terminal (o Visual Studio) para actualizar el `PATH`. |
| Error de SDK no encontrado | Instale la carga de trabajo *Desarrollo de escritorio con .NET* o un SDK que pueda compilar `net8.0-windows` y compruebe `dotnet --list-sdks`. |
| La ventana no aparece | Asegúrese de estar en Windows 10/11; WinForms no se ejecuta en Linux/macOS. |
| No abre el proyecto | Seleccione el archivo `SimuladorAutomatas.csproj`, no la carpeta ni un `.sln`. |
| El depurador no inicia | Compile antes con `Ctrl + Mayús + B` y confirme que existe `bin\Debug\net8.0-windows\SimuladorAutomatas.dll`. |

## 12.6. Tutorial de uso de la aplicación

1. **Abrir la aplicación.** Ejecute `dotnet run` o el archivo `.exe` generado. Se muestra el menú principal con el estado del autómata actual.
2. **Navegar por el menú.** Hay tres botones numerados y un botón *Salir*. Mientras no haya autómata, los módulos 2 y 3 muestran un aviso.
3. **Acceder al constructor.** Pulse **1 CONSTRUIR AUTÓMATA**.
4. **Crear un autómata desde cero.** Escriba exactamente cinco nombres de estados en *Nuevo:* y pulse *Agregar*. Repita para los símbolos en el grupo *Alfabeto Σ*.
5. **Definir estados y alfabeto.** Las listas muestran lo agregado. Los botones *Quitar* eliminan el elemento seleccionado y sus transiciones relacionadas; `Q` debe quedar con cinco nodos.
6. **Establecer inicial y finales.** Elija `q0` en el desplegable *Inicial q0:* y marque las casillas de los estados finales en *Finales F*.
7. **Registrar transiciones.** Escriba el origen, elija el símbolo y el destino y pulse *Agregar δ*. También puede editar las celdas de la rejilla y quitar la fila seleccionada.
8. **Validar y construir.** Pulse **VALIDAR Y CONSTRUIR**. Si falta alguna transición o hay duplicados, se muestra el detalle de errores. Si todo es correcto, el autómata queda disponible para los tres módulos. Como alternativa, elija un caso en *Casos de prueba* y pulse *Carga*.
9. **Interpretar la tabla.** En la pestaña *Tabla de transiciones*, cada fila es un estado y cada columna un símbolo; la celda indica el destino. `→` marca el inicial y `*` los finales.
10. **Interpretar el diagrama.** En la pestaña *Diagrama (GDI+)*, los círculos azul pastel con contorno azul oscuro son estados, la doble circunferencia verde resalta los finales (como `q4`), la etiqueta azul *Inicio* aparece sobre una flecha que sale hacia la derecha desde `q0`, las puntas triangulares muestran el sentido de cada arco curvo y las etiquetas son texto plano sin recuadro. Los bucles son transiciones a uno mismo y se abren hacia el exterior del anillo. Arrastre para desplazarse, use la rueda para ampliar y *Restablecer vista* para volver al centro.
11. **Acceder al módulo de búsqueda.** Cierre el constructor y pulse **2 BUSCAR PALABRAS ACEPTADAS**. La búsqueda se ejecuta al abrir y se puede repetir con *Buscar de nuevo*.
12. **Consultar los resultados.** La tabla muestra hasta cinco palabras en orden de longitud y orden del alfabeto, con su longitud, recorrido y estado final. `ε` aparece cuando es aceptada.
13. **Acceder al módulo de reconocimiento.** Pulse **3 RECONOCER UNA PALABRA**.
14. **Introducir palabras.** Escriba la palabra en el cuadro y pulse **PROCESAR PALABRA** o Enter. Use `ε` para la palabra vacía. El resultado se muestra con colores: verde para aceptada, rojo para rechazada o entrada inválida. *Limpiar* permite otra prueba.
15. **Trabajar con un autómata nuevo.** Vuelva al módulo 1, defina otro AFD y confirme. La nueva instancia sustituye a la anterior en los tres módulos; *Limpiar todo* deja el sistema sin autómata.

## 12.7. Requisitos del sistema

### Requisitos de software

| Concepto | Mínimo | Recomendado |
| --- | --- | --- |
| Sistema operativo | Windows 10 | Windows 11 |
| SDK | SDK de .NET 8 capaz de compilar `net8.0-windows` | SDK de .NET 8 o posterior con componentes Windows Desktop 8.0 |
| Entorno | Terminal de Windows con `dotnet` en el `PATH` | Visual Studio 2022 |
| Extensiones | Ninguna para ejecutar desde terminal | Ninguna; basta la carga de trabajo *Desarrollo de escritorio con .NET* |

Estos valores son requisitos del proyecto, no cifras oficiales de Microsoft.

### Requisitos de hardware

| Concepto | Mínimo orientativo | Recomendación orientativa |
| --- | --- | --- |
| Procesador | 1 GHz | 2 GHz o superior |
| Memoria RAM | 2 GB | 4 GB o más |
| Almacenamiento | 200 MB para SDK, proyectos y archivos generados | 1 GB o más |
| Resolución | 1024 × 768 | 1366 × 768 o superior |

La aplicación es ligera; el consumo real depende del SDK y del entorno de desarrollo instalados.

## 12.8. Estructura del proyecto

```text
SimuladorAutomatas/                   (carpeta raíz del proyecto)
├── Controls/
│   └── GrafoAutomata.cs             Control GDI+ del diagrama
├── Forms/
│   ├── BusquedaPalabras.cs          Módulo 2
│   ├── ConstructorAutomata.cs       Módulo 1
│   ├── MenuPrincipal.cs             Menú y navegación
│   └── ReconocimientoPalabras.cs    Módulo 3
├── Models/
│   ├── Automata.cs                  Quíntupla M = (Q, Σ, δ, q0, F)
│   ├── Estado.cs                    Estado
│   └── Transicion.cs                Transición
├── Pruebas/
│   ├── Pruebas.csproj               Proyecto de consola de verificación
│   └── Program.cs                   55 comprobaciones automáticas
├── Services/
│   ├── AutomataService.cs           Instancia compartida
│   ├── BusquedaPalabrasService.cs   Búsqueda ordenada
│   ├── CasosPruebaService.cs        Cuatro autómatas de prueba
│   ├── ReconocimientoService.cs     Procesamiento de palabras
│   └── ValidacionAutomataService.cs Validación del AFD
├── UI/
│   └── Estilos.cs                   Paleta y controles comunes
├── bin/ y obj/                       Archivos generados por dotnet build
├── Program.cs                        Punto de entrada WinForms
├── README.md                         Esta documentación
└── SimuladorAutomatas.csproj         Configuración .NET 8 + Windows Forms
```

## 12.9. Casos de prueba

### Autómata 1 — Cantidad de ceros ≡ 4 (mod 5)

| Campo | Valor |
| --- | --- |
| Q | `{q0, q1, q2, q3, q4}` |
| Σ | `{0, 1}` |
| q0 | `q0` |
| F | `{q4}` |
| Transiciones | δ(q0,0)=q1, δ(q1,0)=q2, δ(q2,0)=q3, δ(q3,0)=q4, δ(q4,0)=q0; δ(qi,1)=qi para los cinco estados |

| Palabra | Resultado esperado | Verificado |
| --- | --- | --- |
| Cinco más cortas | `0000, 00001, 00010, 00100, 01000` | Sí |
| `0000` | Aceptada (recorrido `q0 → q1 → q2 → q3 → q4`) | Sí |
| `00000` | Rechazada | Sí |
| `ε` | Rechazada | Sí |
| `00001` | Aceptada | Sí |

### Autómata 2 — Palabras que terminan en abab

| Campo | Valor |
| --- | --- |
| Q | `{s0, s1, s2, s3, s4}` |
| Σ | `{a, b}` |
| q0 | `s0` |
| F | `{s4}` |
| Transiciones | δ(s0,a)=s1, δ(s0,b)=s0, δ(s1,a)=s1, δ(s1,b)=s2, δ(s2,a)=s3, δ(s2,b)=s0, δ(s3,a)=s1, δ(s3,b)=s4, δ(s4,a)=s3, δ(s4,b)=s0 |

| Palabra | Resultado esperado | Verificado |
| --- | --- | --- |
| Cinco más cortas | `abab, aabab, babab, aaabab, ababab` | Sí |
| `abab` | Aceptada | Sí |
| `aba` | Rechazada | Sí |
| `babab` | Aceptada | Sí |
| `bb` | Rechazada | Sí |
| `ε` | Rechazada | Sí |
| `abc` | Símbolo inválido `c`, posición 3 | Sí |

### Autómata 3 — Únicamente ab y ba

| Campo | Valor |
| --- | --- |
| Q | `{p0, p1, p2, p3, m}` |
| Σ | `{a, b}` |
| q0 | `p0` |
| F | `{p3}` |
| Transiciones | δ(p0,a)=p1, δ(p0,b)=p2, δ(p1,a)=m, δ(p1,b)=p3, δ(p2,a)=p3, δ(p2,b)=m, δ(p3,a)=m, δ(p3,b)=m, δ(m,a)=m, δ(m,b)=m |

| Prueba | Resultado esperado | Verificado |
| --- | --- | --- |
| Búsqueda | Sólo `ab`, `ba` | Sí |
| `ab`, `ba` | Aceptadas | Sí |
| `aabb`, `ε` | Rechazadas | Sí |

### Autómata 4 — Lenguaje sin palabras aceptadas

| Campo | Valor |
| --- | --- |
| Q | `{q0, q1, q2, q3, q4}` |
| Σ | `{a}` |
| q0 | `q0` |
| F | `{q1, q2}` (inalcanzables) |
| Transiciones | δ(qi, a) = qi para los cinco estados |

| Prueba | Resultado esperado | Verificado |
| --- | --- | --- |
| Búsqueda | Mensaje de «no se encontraron palabras aceptadas» | Sí |
| `aaa`, `ε` | Rechazadas | Sí |

Además se verificaron las condiciones de validación (incluida la exigencia de exactamente cinco nodos), el reemplazo de la quíntupla en el servicio compartido, la creación de los cuatro formularios, el dibujo del diagrama sin excepciones, el acoplamiento del lienzo GDI+ y el margen del texto indicador inicial, el texto visible completo en el cuadro de entrada del Módulo 3, la auditoría de textos sin recortes en el tamaño mínimo de cada ventana y el redimensionado real (tamaño mínimo, maximizado y tamaño grande) sin errores de layout. Comandos ejecutados: `dotnet restore`, `dotnet build` (0 errores y 0 advertencias), `dotnet run --project Pruebas/Pruebas.csproj` (55 comprobaciones, 0 fallos) y arranque del ejecutable en Windows con ventana principal detectada.

## 12.10. Limitaciones y consideraciones

- Sólo se construyen AFD completos y deterministas; no se admite `δ` parcial ni no determinismo.
- `Q` debe tener exactamente cinco nodos; la validación rechaza cualquier otra cantidad.
- Cada símbolo del alfabeto es un carácter individual; los separadores `,`, `;`, `|` y los espacios no forman parte del alfabeto.
- La búsqueda está limitada a longitud 10 y muestra como máximo cinco palabras, según la especificación.
- El recorrido de búsqueda se ordena por longitud y por el orden de introducción de los símbolos; no se ofrece otro criterio de orden.
- La definición `M = (Q, Σ, δ, q0, F)` reside en memoria: al cerrar la aplicación se pierde. No hay guardado ni carga de definiciones desde archivo.
- Los cinco componentes de la quíntupla se guardan juntos en una única instancia de `Automata`, compartida por los tres módulos; no hay estados de aceptación separados.
- La representación gráfica es automática (distribución circular); no se permite arrastrar estados individualmente. Sí se permite desplazar y ampliar el conjunto.
- La aplicación requiere Windows porque utiliza Windows Forms y GDI+.
- El número de palabras de `Σ*` crece muy rápidamente; por eso la búsqueda usa poda basada en estados finales en lugar de enumerar todas las palabras hasta la longitud 10.

## 12.11. Créditos

- Alberto Mario Sáenz Molina
- Aaron Levi Medina Medina
- Camilo Andres Maldonado Duarte
- Jhon Ramírez Salgado
