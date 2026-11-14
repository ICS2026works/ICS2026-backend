# Guía de Contribución y Estrategia de Ramificación

Para mantener la calidad del código y la estabilidad en los entornos, este repositorio aplica una estrategia de ramificación basada en ramas por funcionalidad y revisión obligatoria por pares.

## 1. Estructura de Ramas Principales
 `main` Refleja el código estable listo para producción. Protegida contra commits directos.
 `development` Rama principal de integración para el desarrollo actual. Protegida contra commits directos.

## 2. Nomenclatura para Ramas de Trabajo
Ningún desarrollador debe trabajar directamente en `main` o `development`. Todas las tareas se deben realizar en ramas auxiliares creadas a partir de `development`

 `feature/nombre-tarea` Para el desarrollo de nuevas historias de usuario o funcionalidades (ej. `feature/login-jwt`).
 `hotfix/nombre-correccion` Para correcciones urgentes de errores detectados (ej. `hotfix/fix-cors-error`).
 `test/nombre-tarea` Para el desarrollo de pruebas automatizadas y configuración de CI/CD (ej. `test/setup-infraestructura`, `test/unit-productos`).

## 3. Flujo de Trabajo (Workflow)
1. Posicionarse en `development` y actualizar:
      git checkout development
      git pull origin development
2. Crear la rama de trabajo segun la nomenclatura:
      git checkout -b nomenclatura/nombre-tarea
3. Realizar los cambios, agregarlos y commitear:
      git add .
      git commit -m "mensaje breve que describe el cambio"

CONSEJO: usar `git status` antes del commit o push

4. Subir la rama al repositorio remoto (pagina de GitHub)
      git push origin nomenclatura/nombre-tarea
5. Abrir un Pull Request (PR) desde la nueva rama insertada, hacia `development`, usando la interfaz web de GitHub. 

IMPORTANTE: Fijarse que el destino y la nueva rama a comparar esten en `ICS2026works/`

## 4. reglas de Pull Request y Quality Gate
Para que un Pull Request sea aprobado y pueda mergearse a `development` o `main`, debe cumplir estrictamente con lo siguiente:

`Revision por pares (Peer Review)` El PR debe tener aprobacion de al menos 1 integrante del equipo que no sea el autor del PR
`Quality Gate (Pruebas automatizadas)` El pipeline de GitHub Actions debe finalizar en verde, pasando tanto las pruebas como la construccion docker. Si el step de `dotnet test` falla, el PR no podra ser mergeado. Queda PROHIBIDO forzar el merge si las pruebas estan en rojo.
`Eliminacion de las ramas` Una vez mergeado el PR, la rama de trabajo (feature/, test/, hotfix/) debe eliminarse en el remoto, y en local