PARCHE: corrección de dirección de disparo

Archivo incluido: PlayerShooter.cs

Instalación segura:
1. Cierra Play Mode en Unity si está activo.
2. Haz una copia del archivo actual Assets/_Project/Scripts/PlayerShooter.cs.
3. Copia este PlayerShooter.cs a Assets/_Project/Scripts/ y reemplaza el archivo anterior.
4. Vuelve a Unity y espera a que compile.
5. Selecciona el objeto del jugador y revisa el componente PlayerShooter:
   - Projectile Prefab debe apuntar a Assets/_Project/Prefabs/Projectile.prefab.
   - Muzzle debe apuntar al objeto Muzzle hijo del jugador.
   - Fire Rate puede quedarse en 4.
6. En el prefab Projectile, confirma que Sphere Collider tenga Is Trigger desactivado y que Rigidbody esté activo.
7. En el prefab Enemy, confirma que exista un Collider no trigger en el objeto que contiene Health, o en el mismo objeto donde el código de Projectile puede encontrar IDamageable.

Qué cambia:
- La dirección del disparo usa transform.forward del jugador, que PlayerController orienta hacia el ratón.
- Usa Muzzle solo para la posición, no su rotación local.
- Ignora colisiones entre el proyectil y los colliders del propio jugador.
- Muestra una advertencia si falta Projectile Prefab.

Si el juego usa otro sistema de Input o el jugador visual apunta en un eje distinto del +Z, habrá que ajustar la orientación según la escena.
