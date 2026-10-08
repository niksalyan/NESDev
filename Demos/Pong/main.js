var ballX = 120;
var ballY = 100;

var ballVX = 3;
var ballVY = 3;

var paddleY = 100;
var aiPaddleY = 100;

var paddleY2 = 100;
var aiPaddleY2 = 100;

var paddleY3 = 100;
var aiPaddleY3 = 100;

var score1 = 0;
var score2 = 0;

frame();

for(var x = 0; x < 32; x = x + 1) {
	tileAt(x, 2, 128);
	tileAt(x, 26, 128);
}


while (true) { 

    // --------------------------------
    // BALL MOVEMENT
    // --------------------------------

    ballX = ballX + ballVX;
    ballY = ballY + ballVY;


    // --------------------------------
    // TOP / BOTTOM WALLS
    // --------------------------------

    if (ballY < 20) {
        ballY = 20;
        ballVY = 3;
    }

    if (ballY > 200) {
        ballY = 200;
        ballVY = 0 - 3;
    }


    // --------------------------------
    // PLAYER INPUT
    // --------------------------------

    if (UP1()) {
        if (paddleY > 30) {
            paddleY = paddleY - 5;
        }
    }

    if (DOWN1()) {
        if (paddleY < 185) {
            paddleY = paddleY + 5;
        }
    }


    // --------------------------------
    // SIMPLE AI
    // --------------------------------

    if (ballY < aiPaddleY) {
        if (aiPaddleY > 16) {
            aiPaddleY = aiPaddleY - 2;
        }
    }

    if (ballY > aiPaddleY) {
        if (aiPaddleY < 218) {
            aiPaddleY = aiPaddleY + 2;
        }
    }


    // --------------------------------
    // LEFT PADDLE COLLISION
    // --------------------------------

    if (ballX < 24) {
        if (ballY > paddleY - 12) {
            if (ballY < paddleY + 12) {
                ballX = 24;
                ballVX = 3;
            }
        }
    }


    // --------------------------------
    // RIGHT PADDLE COLLISION
    // --------------------------------

    if (ballX > 232) {
        if (ballY > aiPaddleY - 12) {
            if (ballY < aiPaddleY + 12) {
                ballX = 232;
                ballVX = 0 - 3;
            }
        }
    }


    // --------------------------------
    // RESET IF BALL MISSES A PADDLE
    // --------------------------------

    if (ballX < 0) {
        ballX = 120;
        ballY = 100;
        ballVX = 3;
        ballVY = 3;
    }

    if (ballX > 248) {
        ballX = 120;
        ballY = 100;
        ballVX = 0 - 3;
        ballVY = 3;
    }


	paddleY2 = paddleY - 8;
	paddleY3 = paddleY + 8;
	aiPaddleY2 = aiPaddleY - 8;
	aiPaddleY3 = aiPaddleY + 8;

    // --------------------------------
    // SYNCHRONIZE WITH NEXT FRAME
    // --------------------------------

    frame();
	
	
    // --------------------------------
    // DRAW PLAYER PADDLE
    // --------------------------------

    sprite(1, 128, 16, paddleY);
	sprite(2, 128, 16, paddleY2);
	sprite(3, 128, 16, paddleY3);
    

    // --------------------------------
    // DRAW AI PADDLE
    // --------------------------------

    sprite(5, 128, 240, aiPaddleY);
	sprite(6, 128, 240, aiPaddleY2);
	sprite(7, 128, 240, aiPaddleY3);
    

    // --------------------------------
    // DRAW BALL
    // --------------------------------

    sprite(8, 211, ballX, ballY);
	

}